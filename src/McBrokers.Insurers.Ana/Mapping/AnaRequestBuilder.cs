using System.Globalization;
using System.Xml.Linq;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.Insurers.Abstractions.Soap;

namespace McBrokers.Insurers.Ana.Mapping;

/// <summary>
/// Construye el XML &lt;transacciones&gt; de ANA (tipotransaccion="C") según la documentación
/// "Documentación Servicio ANA — Detalle Técnico Completo.md". La SOAP TransaccionAsync
/// recibe Negocio/Usuario/Clave como parámetros y el xml como string.
/// </summary>
public static class AnaRequestBuilder
{
    public static XNamespace AnaNs => "http://server.anaseguros.com.mx/WSCOR/";

    public static string BuildTransaccionesXml(InsurerQuoteRequest req, string edoMun)
    {
        var transacciones = new XElement("transacciones",
            new XElement("transaccion",
                new XAttribute("version", "1"),
                new XAttribute("tipotransaccion", "C"),
                new XAttribute("cotizacion", ""),
                new XAttribute("negocio", req.Credentials.BusinessUnit ?? string.Empty),
                new XAttribute("tiponegocio", ""),
                BuildVehiculo(req, edoMun),
                new XElement("asegurado",
                    new XAttribute("id", ""),
                    new XAttribute("nombre", ""),
                    new XAttribute("paterno", ""),
                    new XAttribute("materno", ""),
                    new XAttribute("estado", edoMun),
                    new XAttribute("cp", req.PostalCode),
                    new XAttribute("tipopersona", "")),
                new XElement("poliza",
                    new XAttribute("id", ""),
                    new XAttribute("tipo", "A"),
                    new XAttribute("endoso", ""),
                    new XAttribute("vigenciadesde", DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    new XAttribute("vigenciahasta", DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    new XAttribute("forma_pago", MapPaymentMode(req.PaymentMode)),
                    new XAttribute("moneda", "01"))));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), transacciones)
            .ToString(SaveOptions.DisableFormatting);
    }

    public static string BuildSoapEnvelope(InsurerQuoteRequest req, string transaccionesXml)
    {
        var body = new XElement(AnaNs + "Transaccion",
            new XElement(AnaNs + "Negocio", req.Credentials.BusinessUnit ?? string.Empty),
            new XElement(AnaNs + "Usuario", req.Credentials.Username),
            new XElement(AnaNs + "Clave", req.Credentials.Password),
            new XElement(AnaNs + "xml", transaccionesXml));
        return SoapEnvelope.Wrap(SoapVersion.Soap11, body);
    }

    public static string MapPlan(PackageCode package) => package switch
    {
        PackageCode.Amplia => "1",
        PackageCode.Limitada => "3",
        PackageCode.ResponsabilidadCivil => "4",
        _ => "1",
    };

    public static string MapPaymentMode(PaymentMode m) => m switch
    {
        PaymentMode.Annual => "C",
        PaymentMode.Semestral => "S",
        PaymentMode.Trimestral => "T",
        PaymentMode.Monthly => "M",
        PaymentMode.Dxn => "C",        // DXN viaja como Annual en el wire
        _ => "C",
    };

    public static string MapValuation(ValuationType v) => v switch
    {
        ValuationType.Commercial => "3",
        ValuationType.CommercialPlus10 => "1",
        ValuationType.Agreed => "2",
        ValuationType.AgreedPlus10 => "2",
        ValuationType.Invoice => "4",
        _ => "3",
    };

    private static XElement BuildVehiculo(InsurerQuoteRequest req, string edoMun)
    {
        var amis = req.Vehicle.ExternalClave;
        var paquete = MapPlan(req.Package);
        var valorEstimado = MapValuation(req.ValuationType);
        // SumInsured solo viaja cuando ValuationType es Agreed/AgreedPlus10/Invoice.
        // Para Commercial/CommercialPlus10 ANA calcula desde su tarifa interna.
        var sumaVF = req.ValuationType.ShouldSendSumInsured() ? FormatMoney(req.SumInsured) : "0";
        var coverages = BuildCoverages(req, sumaVF, valorEstimado);

        return new XElement("vehiculo",
            new XAttribute("id", "1"),
            new XAttribute("amis", amis),
            new XAttribute("modelo", req.Vehicle.Year.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("descripcion", $"{req.Vehicle.Brand} {req.Vehicle.Model} {req.Vehicle.Version}"),
            new XAttribute("uso", "1"),
            new XAttribute("servicio", "1"),
            new XAttribute("plan", paquete),
            new XAttribute("estado", edoMun),
            new XAttribute("color", "00"),
            coverages);
    }

    private static IEnumerable<XElement> BuildCoverages(InsurerQuoteRequest req, string sumaVF, string valorEstimado)
    {
        // 02 Daños Materiales — Amplia
        // 04 Robo Total — Amplia y Limitada
        // 06 Gastos Médicos, 07 Def. Jud., 10 Asistencia, 25/26 RC Bienes/Personas, 34 RC Catastrófica, 23 RC Ocupantes — todos
        if (req.Package == PackageCode.Amplia)
        {
            yield return Coverage("02", sumaVF, valorEstimado, FormatMoney(req.Deductibles.MaterialDamagesDeductiblePct));
        }
        if (req.Package is PackageCode.Amplia or PackageCode.Limitada)
        {
            yield return Coverage("04", sumaVF, valorEstimado, FormatMoney(req.Deductibles.RobberyDeductiblePct));
        }
        yield return Coverage("06", FormatMoney(req.Deductibles.MedicalExpensesSumInsured), "", "");
        yield return Coverage("07", "", "", "");
        yield return Coverage("10", "", "B", "");
        yield return Coverage("25", "500000", "", "");
        yield return Coverage("26", "500000", "", "");
        yield return Coverage("34", FormatMoney(req.Deductibles.CivilLiabilitySumInsured), "", "");
        yield return Coverage("23", "300000", "", "");
    }

    private static XElement Coverage(string id, string sa, string tipo, string ded) =>
        new("cobertura",
            new XAttribute("id", id),
            new XAttribute("desc", ""),
            new XAttribute("sa", sa),
            new XAttribute("tipo", tipo),
            new XAttribute("ded", ded),
            new XAttribute("pma", ""));

    private static string FormatMoney(decimal amount) =>
        amount.ToString("0.##", CultureInfo.InvariantCulture);
}
