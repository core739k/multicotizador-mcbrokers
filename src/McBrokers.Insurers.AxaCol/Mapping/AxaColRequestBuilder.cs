using System.Globalization;
using System.Text;
using System.Xml.Linq;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;

namespace McBrokers.Insurers.AxaCol.Mapping;

/// <summary>
/// AXA COL — el XML de Solicitud va embebido en CDATA dentro del envelope SOAP.
/// Construido directamente como string para preservar el CDATA literalmente.
/// </summary>
public static class AxaColRequestBuilder
{
    public const string WsfNamespace = "http://wsfacade.emisionpolizas.autos.seguros.mx.ia3.ing.com";

    public static string BuildSolicitudXml(InsurerQuoteRequest req, DateOnly today)
    {
        var endDate = today.AddYears(1);
        var dateFmt = today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var endFmt = endDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var age = CalculateAge(today, req.HabitualDriver.DateOfBirth);
        // SumInsured solo viaja cuando ValuationType es Agreed/AgreedPlus10/Invoice.
        // Para Commercial/CommercialPlus10 AXA COL calcula desde su tarifa interna.
        var sumaVF = req.ValuationType.ShouldSendSumInsured() ? FormatMoney(req.SumInsured) : "0";
        var tipoValor = MapValuation(req.ValuationType);
        var coberturas = BuildCoverages(req);

        var solicitud = new XElement("Solicitud",
            new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            new XAttribute(XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance") + "noNamespaceSchemaLocation",
                "https://portal.axa.com.mx/XSD/Solicitud.xsd"),
            new XElement("Poliza",
                new XElement("NombreProducto", req.Credentials.BusinessUnit ?? string.Empty),
                new XElement("Generales",
                    new XElement("TipoPoliza", "COLECTIVA"),
                    new XElement("TipoMovimiento", "COTIZACION"),
                    new XElement("SeriePoliza", req.Credentials.BusinessUnit ?? string.Empty),
                    new XElement("TipoVigencia", "ANUAL"),
                    new XElement("FechaDesde", dateFmt),
                    new XElement("FechaHasta", endFmt),
                    new XElement("Titular",
                        new XElement("TipoPersona", "FISICA"),
                        new XElement("Nombre", req.Contractor.FirstName),
                        new XElement("ApPaterno", req.Contractor.LastNamePaternal),
                        new XElement("ApMaterno", req.Contractor.LastNameMaternal),
                        new XElement("RFC", "XXAX010101XXX"),
                        new XElement("Telefono", "8080808080"),
                        new XElement("Direccion",
                            new XElement("CalleNumero", "CALLE 1"),
                            new XElement("CodigoPostal", req.PostalCode)),
                        new XElement("Sexo", req.HabitualDriver.Gender == Gender.Female ? "FEMENINO" : "MASCULINO"),
                        new XElement("Edad", age),
                        new XElement("TieneGarage", "NO"),
                        new XElement("EstacionamientoOficina", "NO"),
                        new XElement("EstadoCivil", "SOLTERO")),
                    new XElement("FormaPago", MapPaymentMode(req.PaymentMode)),
                    new XElement("Moneda", "NACIONAL"),
                    new XElement("IVA", "16"),
                    new XElement("TipoNegocio", "NORMAL"),
                    new XElement("Subramo", "AUTOS"),
                    new XElement("TipoVehiculo", "RESIDENTES"),
                    new XElement("Vehiculo",
                        new XElement("Marca", req.Vehicle.Brand),
                        new XElement("Tipo", req.Vehicle.Model),
                        new XElement("Modelo", req.Vehicle.Year.ToString(CultureInfo.InvariantCulture)),
                        new XElement("ClaveAMIS", req.Vehicle.ExternalClave),
                        new XElement("Descripcion", req.Vehicle.Version),
                        new XElement("Uso", "PARTICULAR"),
                        new XElement("Servicio", "PARTICULAR"),
                        new XElement("TipoValor", tipoValor),
                        new XElement("ValorComercial", sumaVF),
                        coberturas))));

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), solicitud);
        return doc.ToString(SaveOptions.DisableFormatting);
    }

    public static string BuildSoapEnvelope(string solicitudXml)
    {
        // Construimos el SOAP envelope manualmente para preservar el CDATA exactamente como lo espera AXA.
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.Append("<soapenv:Envelope ");
        sb.Append("xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" ");
        sb.Append("xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" ");
        sb.Append("xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" ");
        sb.Append($"xmlns:wsf=\"{WsfNamespace}\">");
        sb.Append("<soapenv:Header/>");
        sb.Append("<soapenv:Body>");
        sb.Append("<wsf:createSolicitudPolizasInmediata soapenv:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">");
        sb.Append("<xml xsi:type=\"xsd:string\"><![CDATA[");
        sb.Append(solicitudXml);
        sb.Append("]]></xml>");
        sb.Append("</wsf:createSolicitudPolizasInmediata>");
        sb.Append("</soapenv:Body>");
        sb.Append("</soapenv:Envelope>");
        return sb.ToString();
    }

    public static int CalculateAge(DateOnly today, DateOnly birthDate)
    {
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age)) age--;
        return Math.Max(0, age);
    }

    public static string MapPaymentMode(PaymentMode m) => m switch
    {
        PaymentMode.Annual => "CONTADO",
        PaymentMode.Semestral => "SEMESTRAL",
        PaymentMode.Trimestral => "TRIMESTRAL",
        PaymentMode.Monthly => "MENSUAL",
        PaymentMode.Dxn => "CONTADO",  // DXN viaja como Annual en el wire
        _ => "CONTADO",
    };

    public static string MapValuation(ValuationType v) => v switch
    {
        ValuationType.Commercial => "COMERCIAL",
        ValuationType.CommercialPlus10 => "COMERCIAL_PLUS10",
        ValuationType.Agreed => "CONVENIDO",
        ValuationType.AgreedPlus10 => "CONVENIDO_PLUS10",
        ValuationType.Invoice => "FACTURA",
        _ => "COMERCIAL",
    };

    private static IEnumerable<XElement> BuildCoverages(InsurerQuoteRequest req)
    {
        if (req.Package == PackageCode.Amplia)
        {
            yield return Coverage("DM", FormatMoney(req.Deductibles.MaterialDamagesDeductiblePct));
        }
        if (req.Package is PackageCode.Amplia or PackageCode.Limitada)
        {
            yield return Coverage("RT", FormatMoney(req.Deductibles.RobberyDeductiblePct));
        }
        yield return Coverage("RC", FormatMoney(req.Deductibles.CivilLiabilitySumInsured / 1000m));
        yield return Coverage("GMO", FormatMoney(req.Deductibles.MedicalExpensesSumInsured));
    }

    private static XElement Coverage(string clave, string value) =>
        new("Cobertura",
            new XElement("ClaveCobertura", clave),
            new XElement("Valor", value));

    private static string FormatMoney(decimal amount) =>
        amount.ToString("0.##", CultureInfo.InvariantCulture);
}
