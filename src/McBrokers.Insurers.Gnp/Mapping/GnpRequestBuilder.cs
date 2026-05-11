using System.Globalization;
using System.Text;
using System.Xml.Linq;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;

namespace McBrokers.Insurers.Gnp.Mapping;

/// <summary>
/// Construye el XML <COTIZACION> de GNP según
/// <c>Documentación Servicio GNP — Detalle Técnico Completo.md</c>.
/// </summary>
public static class GnpRequestBuilder
{
    public static string BuildQuoteRequest(InsurerQuoteRequest req, DateOnly today)
    {
        var endDate = today.AddYears(1);
        var clave = req.Vehicle.ExternalClave ?? string.Empty;
        var (armadora, carroceria, version) = DecodeAmisClave(clave);

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("COTIZACION",
                new XElement("SOLICITUD",
                    new XElement("NUM_COTIZACION", string.Empty),
                    new XElement("USUARIO", req.Credentials.Username),
                    new XElement("PASSWORD", req.Credentials.Password),
                    new XElement("ID_UNIDAD_OPERABLE", req.Credentials.BusinessUnit ?? string.Empty),
                    new XElement("FCH_INICIO_VIGENCIA", today.ToString("yyyyMMdd", CultureInfo.InvariantCulture)),
                    new XElement("FCH_FIN_VIGENCIA", endDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)),
                    new XElement("VIA_PAGO", "IN"),
                    new XElement("PERIODICIDAD", MapPeriodicity(req.PaymentMode)),
                    new XElement("ELEMENTOS",
                        new XElement("ELEMENTO",
                            new XElement("NOMBRE", "CODIGO_PROMOCION"),
                            new XElement("CLAVE", "COP0000202"),
                            new XElement("VALOR", "COP0000202")))),
                new XElement("VEHICULO",
                    new XElement("SUB_RAMO", "01"),
                    new XElement("TIPO_VEHICULO", "AUT"),
                    new XElement("MODELO", req.Vehicle.Year.ToString(CultureInfo.InvariantCulture)),
                    new XElement("ARMADORA", armadora),
                    new XElement("CARROCERIA", carroceria),
                    new XElement("VERSION", version),
                    new XElement("USO", "01"),
                    new XElement("FORMA_INDEMNIZACION", MapValuation(req.ValuationType)),
                    new XElement("VALOR_FACTURA", FormatMoney(req.SumInsured))),
                new XElement("CONTRATANTE",
                    new XElement("TIPO_PERSONA", "F"),
                    new XElement("CODIGO_POSTAL", req.PostalCode),
                    new XElement("APELLIDO_PATERNO", RemoveAccents(req.Contractor.LastNamePaternal)),
                    new XElement("APELLIDO_MATERNO", RemoveAccents(req.Contractor.LastNameMaternal)),
                    new XElement("NOMBRE", RemoveAccents(req.Contractor.FirstName))),
                new XElement("CONDUCTOR",
                    new XElement("FCH_NACIMIENTO", req.HabitualDriver.DateOfBirth.ToString("yyyyMMdd", CultureInfo.InvariantCulture)),
                    new XElement("SEXO", req.HabitualDriver.Gender == Gender.Female ? "F" : "M"),
                    new XElement("EDAD", AgeOn(today, req.HabitualDriver.DateOfBirth).ToString(CultureInfo.InvariantCulture)),
                    new XElement("CODIGO_POSTAL", req.HabitualDriver.PostalCode)),
                BuildPackage(req)));

        return doc.Declaration + doc.ToString(SaveOptions.DisableFormatting);
    }

    public static (string Armadora, string Carroceria, string Version) DecodeAmisClave(string clave)
    {
        // Posición 3-4 → ARMADORA, 5-6 → CARROCERIA, 7-8 → VERSION (cero-indexada).
        if (string.IsNullOrEmpty(clave) || clave.Length < 9)
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        return (clave.Substring(3, 2), clave.Substring(5, 2), clave.Substring(7, 2));
    }

    public static string MapPeriodicity(PaymentMode mode) => mode switch
    {
        PaymentMode.Annual => "A",
        PaymentMode.Semestral => "S",
        PaymentMode.Trimestral => "T",
        PaymentMode.Monthly => "M",
        _ => "A",
    };

    public static string MapValuation(ValuationType valuation) => valuation switch
    {
        ValuationType.Commercial => "01",
        ValuationType.CommercialPlus10 => "08",
        ValuationType.Agreed => "03",
        ValuationType.AgreedPlus10 => "04",
        ValuationType.Invoice => "02",
        _ => "01",
    };

    public static int AgeOn(DateOnly today, DateOnly birthDate)
    {
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age)) age--;
        return Math.Max(0, age);
    }

    public static string RemoveAccents(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string FormatMoney(decimal amount) =>
        amount.ToString("0.##", CultureInfo.InvariantCulture);

    private static XElement BuildPackage(InsurerQuoteRequest req)
    {
        var coverages = new List<XElement>();
        var deductibles = req.Deductibles;
        var package = req.Package;

        // Solo AMPLIA: DM Pérdida Total + DM Pérdida Parcial
        if (package == PackageCode.Amplia)
        {
            coverages.Add(CoverageWithDeductible("0000001288", deductibles.MaterialDamagesDeductiblePct));
            coverages.Add(CoverageWithDeductible("0000001289", deductibles.MaterialDamagesDeductiblePct));
        }

        // AMPLIA + LIMITADA: Robo Total
        if (package == PackageCode.Amplia || package == PackageCode.Limitada)
        {
            coverages.Add(CoverageWithDeductible("0000000916", deductibles.RobberyDeductiblePct));
        }

        // Todos los paquetes: GMO, RC, PL, Extensión RC
        coverages.Add(CoverageWithSum("0000000906", deductibles.MedicalExpensesSumInsured));
        coverages.Add(CoverageWithSum("0000001273", deductibles.CivilLiabilitySumInsured));
        coverages.Add(CoverageWithSum("0000001285", deductibles.CivilLiabilitySumInsured));
        coverages.Add(CoverageWithSum("0000000904", deductibles.CivilLiabilitySumInsured));

        return new XElement("PAQUETES",
            new XElement("PAQUETE",
                new XElement("CVE_PAQUETE", req.PackageExternalCode),
                new XElement("DESC_PAQUETE", DescribePackage(package)),
                new XElement("COBERTURAS", coverages)));
    }

    private static string DescribePackage(PackageCode p) => p switch
    {
        PackageCode.Amplia => "AMPLIA",
        PackageCode.Limitada => "LIMITADA",
        PackageCode.ResponsabilidadCivil => "RESPONSABILIDAD CIVIL",
        _ => "AMPLIA",
    };

    private static XElement CoverageWithDeductible(string code, decimal deductible) =>
        new("COBERTURA",
            new XElement("CVE_COBERTURA", code),
            new XElement("DEDUCIBLE", FormatMoney(deductible)),
            new XElement("PRIMA", string.Empty));

    private static XElement CoverageWithSum(string code, decimal sumInsured) =>
        new("COBERTURA",
            new XElement("CVE_COBERTURA", code),
            new XElement("SUMA_ASEGURADA", FormatMoney(sumInsured)),
            new XElement("DEDUCIBLE", string.Empty));
}
