using System.Globalization;
using System.Xml.Linq;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.Insurers.Abstractions.Soap;

namespace McBrokers.Insurers.Qualitas.Mapping;

/// <summary>
/// Construye el XML &lt;Movimientos&gt; de Quálitas (TipoMovimiento="2" para cotización) según
/// "Documentación Servicio Qualitas — Detalle Técnico Completo.md".
/// </summary>
public static class QualitasRequestBuilder
{
    public static XNamespace QualitasNs => "http://qbcenter.qualitas.com.mx/wsCotQua/";

    public static string BuildMovimientosXml(InsurerQuoteRequest req, DateOnly today)
    {
        var endDate = today.AddYears(1);
        var dateFmt = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var endFmt = endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var movimientos = new XElement("Movimientos",
            new XElement("Movimiento",
                new XAttribute("NoNegocio", req.Credentials.BusinessUnit ?? string.Empty),
                new XAttribute("NoOTra", ""),
                new XAttribute("TipoEndoso", ""),
                new XAttribute("NoEndoso", ""),
                new XAttribute("NoCotizacion", ""),
                new XAttribute("NoPoliza", ""),
                new XAttribute("TipoMovimiento", "2"),
                BuildDatosAsegurado(req),
                BuildDatosVehiculo(req),
                BuildDatosGenerales(req, dateFmt, endFmt),
                new XElement("Primas",
                    new XElement("PrimaNeta"),
                    new XElement("Derecho", "0"),
                    new XElement("Recargo"),
                    new XElement("Impuesto"),
                    new XElement("PrimaTotal"),
                    new XElement("Comision")),
                new XElement("CodigoError")));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), movimientos)
            .ToString(SaveOptions.DisableFormatting);
    }

    public static string BuildSoapBody(string movimientosXml)
    {
        var body = new XElement(QualitasNs + "obtenerNuevaEmision",
            new XElement(QualitasNs + "XmlCotiza", movimientosXml));
        return SoapEnvelope.Wrap(SoapVersion.Soap12, body);
    }

    public static int CalculateAmisVerifier(string amis)
    {
        if (string.IsNullOrEmpty(amis) || amis.Length < 5)
        {
            return 0;
        }
        // De la doc: 1·3 + 2 + 3·3 + 4 + 5·3 — sumar y completar a múltiplo de 10.
        int Non1 = ToDigit(amis[0]);
        int Par1 = ToDigit(amis[1]);
        int Non2 = ToDigit(amis[2]);
        int Par2 = ToDigit(amis[3]);
        int Non3 = ToDigit(amis[4]);
        int suma = ((Non1 + Non2 + Non3) * 3) + (Par1 + Par2);
        int digito = 0;
        while ((suma % 10) != 0) { digito++; suma++; }
        return digito;
    }

    private static int ToDigit(char c) => char.IsDigit(c) ? c - '0' : 0;

    private static XElement BuildDatosAsegurado(InsurerQuoteRequest req) =>
        new("DatosAsegurado",
            new XAttribute("NoAsegurado", ""),
            new XElement("Nombre"),
            new XElement("Direccion"),
            new XElement("Colonia"),
            new XElement("Poblacion"),
            new XElement("Estado"),
            new XElement("CodigoPostal", req.PostalCode),
            new XElement("NoEmpleado"),
            new XElement("Agrupador"));

    private static XElement BuildDatosVehiculo(InsurerQuoteRequest req)
    {
        var amis = req.Vehicle.ExternalClave;
        var paquete = MapPackage(req.Package);
        var coberturas = BuildCoverages(req);

        return new XElement("DatosVehiculo",
            new XAttribute("NoInciso", "1"),
            new XElement("ClaveAmis", amis),
            new XElement("Modelo", req.Vehicle.Year.ToString(CultureInfo.InvariantCulture)),
            new XElement("DescripcionVehiculo", $"{req.Vehicle.Model} {req.Vehicle.Version}"),
            new XElement("Uso", DetectUso(amis)),
            new XElement("Servicio", "1"),
            new XElement("Paquete", paquete),
            new XElement("Motor"),
            new XElement("Serie"),
            coberturas);
    }

    private static string MapPackage(PackageCode p) => p switch
    {
        PackageCode.Amplia => "1",
        PackageCode.Limitada => "2",
        PackageCode.ResponsabilidadCivil => "3",
        _ => "1",
    };

    private static string DetectUso(string? amis)
    {
        if (!string.IsNullOrEmpty(amis) && amis.StartsWith('5')) return "05";
        return "1";
    }

    private static IEnumerable<XElement> BuildCoverages(InsurerQuoteRequest req)
    {
        var sumaVF = FormatMoney(req.SumInsured);
        var tipoSuma = MapValuation(req.ValuationType);

        // 01 Daños Materiales — solo Amplia
        if (req.Package == PackageCode.Amplia)
        {
            yield return Coverage("01", sumaVF, tipoSuma, FormatMoney(req.Deductibles.MaterialDamagesDeductiblePct));
        }
        // 03 Robo Total — Amplia y Limitada
        if (req.Package is PackageCode.Amplia or PackageCode.Limitada)
        {
            yield return Coverage("03", sumaVF, tipoSuma, FormatMoney(req.Deductibles.RobberyDeductiblePct));
        }
        // Coberturas fijas para todos los paquetes
        yield return Coverage("04", FormatMoney(req.Deductibles.CivilLiabilitySumInsured), "0", "0");
        yield return Coverage("05", FormatMoney(req.Deductibles.MedicalExpensesSumInsured), "0", "0");
        yield return Coverage("07", "0", "0", "0");
        yield return Coverage("11", "0", "0", "0");
        yield return Coverage("14", "", "0", "0");
        yield return Coverage("22", "0", "0", "0");
    }

    private static string MapValuation(ValuationType v) => v switch
    {
        ValuationType.Commercial => "3",
        ValuationType.CommercialPlus10 => "3",
        ValuationType.Agreed => "0",
        ValuationType.AgreedPlus10 => "0",
        ValuationType.Invoice => "1",
        _ => "2",
    };

    private static XElement Coverage(string noCobertura, string sa, string tipoSuma, string deducible) =>
        new("Coberturas",
            new XAttribute("NoCobertura", noCobertura),
            new XElement("TipoSuma", tipoSuma),
            new XElement("SumaAsegurada", sa),
            new XElement("Deducible", deducible),
            new XElement("Prima", "0"));

    private static XElement BuildDatosGenerales(InsurerQuoteRequest req, string today, string end)
    {
        var amisDigit = CalculateAmisVerifier(req.Vehicle.ExternalClave);

        return new XElement("DatosGenerales",
            new XElement("FechaEmision", today),
            new XElement("FechaInicio", today),
            new XElement("FechaTermino", end),
            new XElement("Moneda", "0"),
            new XElement("Agente", req.Credentials.Username),
            new XElement("FormaPago", MapPaymentMode(req.PaymentMode)),
            new XElement("TarifaValores", "LINEA"),
            new XElement("TarifaCuotas", "LINEA"),
            new XElement("TarifaDerechos", "LINEA"),
            new XElement("Plazo"),
            new XElement("Agencia"),
            new XElement("Contrato"),
            new XElement("PorcentajeDescuento", "0"),
            new XElement("ConsideracionesAdicionalesDG",
                new XAttribute("NoConsideracion", "01"),
                new XElement("TipoRegla", "1"),
                new XElement("ValorRegla", amisDigit)),
            new XElement("ConsideracionesAdicionalesDG",
                new XAttribute("NoConsideracion", "04"),
                new XElement("TipoRegla", "1"),
                new XElement("ValorRegla", "0")));
    }

    public static string MapPaymentMode(PaymentMode m) => m switch
    {
        PaymentMode.Annual => "C",
        PaymentMode.Semestral => "S",
        PaymentMode.Trimestral => "T",
        PaymentMode.Monthly => "M",
        PaymentMode.Dxn => "C",        // DXN viaja como Annual en el wire
        _ => "C",
    };

    private static string FormatMoney(decimal amount) =>
        amount.ToString("0.##", CultureInfo.InvariantCulture);
}
