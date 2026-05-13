using System.Globalization;
using System.Xml.Linq;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.Insurers.Abstractions.Soap;

namespace McBrokers.Insurers.AxaDxn.Mapping;

/// <summary>
/// AXA DXN (Flotillas/Residentes) — namespace http://axa.com.mx/autos/flotillas/ws.
/// La cotización envía datosSolicitud con datosPoliza + datosVehiculo + datosCoberturas.
/// Emisión (F5) va por COPSIS — fuera del alcance de F4.
/// </summary>
public static class AxaDxnRequestBuilder
{
    public const string FlotillasNamespace = "http://axa.com.mx/autos/flotillas/ws";

    public static string BuildSoapEnvelope(InsurerQuoteRequest req, AxaDxnAdapterConfig axa, DateOnly today)
    {
        XNamespace ws = FlotillasNamespace;
        var body = new XElement(ws + "CotizarIncisoRequest",
            new XElement("datosSolicitud",
                BuildDatosPoliza(req, axa, today),
                BuildDatosVehiculo(req, today),
                BuildDatosCoberturas(req, axa)));

        return SoapEnvelope.Wrap(SoapVersion.Soap11, body);
    }

    public static int CalculateAge(DateOnly today, DateOnly birthDate)
    {
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age)) age--;
        return Math.Max(0, age);
    }

    public static string MapValuationDescriptor(ValuationType v) => v switch
    {
        ValuationType.Commercial => "Comercial",
        ValuationType.CommercialPlus10 => "Comercial",
        ValuationType.Agreed => "Convenido",
        ValuationType.AgreedPlus10 => "Convenido",
        ValuationType.Invoice => "Factura",
        _ => "Comercial",
    };

    public static string MapValuationPercentage(ValuationType v) => v switch
    {
        ValuationType.CommercialPlus10 or ValuationType.AgreedPlus10 => "110",
        _ => "100",
    };

    private static XElement BuildDatosPoliza(InsurerQuoteRequest req, AxaDxnAdapterConfig axa, DateOnly today) =>
        new("datosPoliza",
            // numeroPoliza viene del negocio seleccionado (STRM/CAJA/MCB/...) — autos por default.
            // Si el vehículo fuera pickup el ProcessQuotation debería pasar PolizaPickup;
            // por simplicidad de POC usamos PolizaAutos y fallback a vacío.
            new XElement("numeroPoliza", axa.PolizaAutos ?? string.Empty),
            new XElement("vigenciaDesde", today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
            new XElement("porcentajeDescuento", axa.Descuento.ToString(CultureInfo.InvariantCulture)),
            new XElement("tipoNegocio", "Normal"),
            new XElement("modoPago", "Efectivo"),
            new XElement("miembroDesde", "0"));

    private static XElement BuildDatosVehiculo(InsurerQuoteRequest req, DateOnly today)
    {
        var amis = req.Vehicle.ExternalClave ?? string.Empty;
        var marca = amis.Length >= 3 ? amis[..3] : amis.PadRight(3, '0');
        var tipo = amis.Length >= 5 ? amis[^2..] : "00";

        return new XElement("datosVehiculo",
            new XElement("modelo", req.Vehicle.Year.ToString(CultureInfo.InvariantCulture)),
            new XElement("marca", marca),
            new XElement("tipo", tipo),
            new XElement("clase", "1"),
            new XElement("descripcionVehiculo", $"{req.Vehicle.Brand} {req.Vehicle.Model} {req.Vehicle.Version}"),
            new XElement("sumaEquipoEspecial", "0"),
            new XElement("sumaAdaptaciones", "0"),
            new XElement("conductorHabitual", "true"),
            new XElement("sexoConductor", req.HabitualDriver.Gender == Gender.Female ? "2" : "1"),
            new XElement("edadConductor", CalculateAge(today, req.HabitualDriver.DateOfBirth)),
            new XElement("codigoPostal", req.PostalCode));
    }

    private static XElement BuildDatosCoberturas(InsurerQuoteRequest req, AxaDxnAdapterConfig axa)
    {
        // SumInsured solo viaja cuando ValuationType es Agreed/AgreedPlus10/Invoice.
        // Para Commercial/CommercialPlus10 AXA DXN calcula desde su tarifa interna.
        var sumaVF = req.ValuationType.ShouldSendSumInsured() ? FormatMoney(req.SumInsured) : "0";
        var tipoValor = MapValuationDescriptor(req.ValuationType);
        var pctValor = MapValuationPercentage(req.ValuationType);
        var sumaRcMiles = FormatMoney(req.Deductibles.CivilLiabilitySumInsured / 1000m);

        var datos = new XElement("datosCoberturas",
            new XElement("afianzadora", "6"),
            new XElement("servidora", "5"),
            new XElement("tipoValor", tipoValor),
            new XElement("porcentajeValor", pctValor),
            new XElement("valorUnidad", sumaVF),
            new XElement("servicio", "Particular"),
            new XElement("tipoCarga", "Normal"),
            new XElement("benefAccidentes", "NO"),
            new XElement("uso", "Normal"),
            new XElement("viajeros", "0"),
            new XElement("suva", "NO"),
            new XElement("dobleRemolque", "NO"),
            new XElement("arrastre", "NO"),
            new XElement("tipoDeducible", "Variable"));

        // DM + RT condicionales
        if (req.Package == PackageCode.Amplia)
        {
            datos.Add(CoverageWithDeductible("DM", req.Deductibles.MaterialDamagesDeductiblePct));
        }
        if (req.Package is PackageCode.Amplia or PackageCode.Limitada)
        {
            datos.Add(CoverageWithDeductible("RT", req.Deductibles.RobberyDeductiblePct));
        }
        // Fijas
        datos.Add(CoverageWithSum("RC", "0", sumaRcMiles));
        datos.Add(CoverageWithSumOnly("RCP", "3000"));
        datos.Add(CoverageWithSumOnly("GMO", "60000"));
        datos.Add(new XElement("coberturasAmparar",
            new XElement("claveCobertura", "SRV")));
        datos.Add(CoverageWithSumOnly("DL", sumaRcMiles));
        datos.Add(CoverageWithSumOnly("ACC", "100000"));

        return datos;
    }

    private static XElement CoverageWithDeductible(string clave, decimal deducible) =>
        new("coberturasAmparar",
            new XElement("claveCobertura", clave),
            new XElement("deducible", FormatMoney(deducible)));

    private static XElement CoverageWithSum(string clave, string deducible, string suma) =>
        new("coberturasAmparar",
            new XElement("claveCobertura", clave),
            new XElement("deducible", deducible),
            new XElement("sumaAsegurada", suma));

    private static XElement CoverageWithSumOnly(string clave, string suma) =>
        new("coberturasAmparar",
            new XElement("claveCobertura", clave),
            new XElement("sumaAsegurada", suma));

    private static string FormatMoney(decimal amount) =>
        amount.ToString("0.##", CultureInfo.InvariantCulture);
}
