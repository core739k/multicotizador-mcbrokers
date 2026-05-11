using System.Globalization;
using System.Xml.Linq;
using McBrokers.Insurers.Abstractions;

namespace McBrokers.Insurers.Gnp.Mapping;

/// <summary>
/// Construye el XML &lt;EMISION&gt; de GNP según la documentación. Mismo estilo posicional para
/// armadora/carrocería/versión que cotización; pero ahora se llenan vehículo (placas, motor, serie),
/// contratante completo (RFC, dirección, teléfono, correo) e importes obtenidos en cotización.
/// </summary>
public static class GnpEmissionBuilder
{
    public static string BuildEmissionXml(InsurerEmitRequest req, DateOnly today)
    {
        var endDate = today.AddYears(1);
        var clave = req.Vehicle.ExternalClave ?? string.Empty;
        var (armadora, carroceria, version) = GnpRequestBuilder.DecodeAmisClave(clave);

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("EMISION",
                BuildSolicitud(req, today, endDate),
                new XElement("AGENTES"),
                BuildVehiculo(req, armadora, carroceria, version),
                BuildContratante(req),
                BuildConductor(req),
                BuildBeneficiarios(),
                new XElement("PAQUETE",
                    new XElement("CVE_PAQUETE", string.Empty)),
                new XElement("IMPORTES",
                    new XElement("PRIMA_TOTAL", FormatMoney(req.PremiumTotal)),
                    new XElement("IMP_IVA", FormatMoney(req.Tax)),
                    new XElement("PRIMA_NETA", FormatMoney(req.PremiumNet)))));

        return doc.Declaration + doc.ToString(SaveOptions.DisableFormatting);
    }

    public static string BuildPrintRequest(string usuario, string password, string policyNumber)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("IMPRESION_POLIZA",
                new XElement("USUARIO", usuario),
                new XElement("PASSWORD", password),
                new XElement("NUM_POLIZA", policyNumber),
                new XElement("NUM_VERSION", "0"),
                new XElement("EXTENSION_ARCHIVO", "PDF")));
        return doc.Declaration + doc.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement BuildSolicitud(InsurerEmitRequest req, DateOnly today, DateOnly endDate) =>
        new("SOLICITUD",
            new XElement("USUARIO", req.Credentials.Username),
            new XElement("PASSWORD", req.Credentials.Password),
            new XElement("ID_UNIDAD_OPERABLE", req.Credentials.BusinessUnit ?? string.Empty),
            new XElement("NUM_COTIZACION", req.ExternalQuoteRef),
            new XElement("FCH_INICIO_VIGENCIA", today.ToString("yyyyMMdd", CultureInfo.InvariantCulture)),
            new XElement("FCH_FIN_VIGENCIA", endDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)),
            new XElement("FCH_EFECTO_MOVIMIENTO", today.ToString("yyyyMMdd", CultureInfo.InvariantCulture)),
            new XElement("FCH_FIN_EFECTO_MOVIMIENTO", endDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)),
            new XElement("VIA_PAGO", "IN"),
            new XElement("VIA_PAGO_SUCESIVOS", "IN"),
            new XElement("PERIODICIDAD", "A"),
            new XElement("CVE_MONEDA", "MXN"),
            new XElement("BAN_RENOVACION_AUTOMATICA", "1"),
            new XElement("BAN_URL_IMPRESION", "0"),
            new XElement("CVE_FORMA_AJUSTE_IRREGULAR", "PR"),
            new XElement("BAN_CONTRA_IGUAL_CONDUCTOR", "1"),
            new XElement("BAN_CONTRA_IGUAL_BENEFICIARIO", "1"),
            new XElement("BAN_AFECTA_BONO", "0"),
            new XElement("OPERACION", "E"),
            new XElement("ELEMENTOS",
                new XElement("ELEMENTO",
                    new XElement("NOMBRE", "CODIGO_PROMOCION"),
                    new XElement("CLAVE", "COP0000202"),
                    new XElement("VALOR", "COP0000202"))));

    private static XElement BuildVehiculo(InsurerEmitRequest req, string armadora, string carroceria, string version) =>
        new("VEHICULO",
            new XElement("SUB_RAMO", "01"),
            new XElement("TIPO_VEHICULO", "AUT"),
            new XElement("MODELO", req.Vehicle.Year.ToString(CultureInfo.InvariantCulture)),
            new XElement("ARMADORA", armadora),
            new XElement("CARROCERIA", carroceria),
            new XElement("VERSION", version),
            new XElement("USO", "01"),
            new XElement("PLACAS", GnpRequestBuilder.RemoveAccents(req.Vehicle.Plate)),
            new XElement("ALTO_RIESGO", "0"),
            new XElement("TIPO_CARGA", ""),
            new XElement("MOTOR", GnpRequestBuilder.RemoveAccents(req.Vehicle.EngineNumber)),
            new XElement("SERIE", req.Vehicle.SerialNumber),
            new XElement("CODIGO_POSTAL", req.Contractor.PostalCode));

    private static XElement BuildContratante(InsurerEmitRequest req)
    {
        var phoneDigits = new string(req.Contractor.Phone.Where(char.IsDigit).ToArray());
        var lada = phoneDigits.Length >= 2 ? phoneDigits[..2] : "00";
        var numero = phoneDigits.Length >= 8 ? phoneDigits[^8..] : phoneDigits.PadLeft(8, '0');

        return new XElement("CONTRATANTE",
            new XElement("TIPO_PERSONA", "F"),
            new XElement("RFC", req.Contractor.Rfc),
            new XElement("NOMBRES", GnpRequestBuilder.RemoveAccents(req.Contractor.FirstName)),
            new XElement("APELLIDO_PATERNO", GnpRequestBuilder.RemoveAccents(req.Contractor.LastNamePaternal)),
            new XElement("APELLIDO_MATERNO", GnpRequestBuilder.RemoveAccents(req.Contractor.LastNameMaternal)),
            new XElement("SEXO", "M"),
            new XElement("ESTADO_CIVIL", "S"),
            new XElement("NACIONALIDAD", "MEX"),
            new XElement("PAIS_NACIMIENTO", "MEX"),
            new XElement("DIRECCION",
                new XElement("CVE_TIPO_VIA", "CL"),
                new XElement("CALLE", GnpRequestBuilder.RemoveAccents(req.Contractor.Street)),
                new XElement("NUMERO_EXTERIOR", req.Contractor.ExteriorNumber),
                new XElement("NUMERO_INTERIOR", req.Contractor.InteriorNumber ?? string.Empty),
                new XElement("COLONIA", GnpRequestBuilder.RemoveAccents(req.Contractor.Neighborhood)),
                new XElement("DELEGACION_MCPIO", GnpRequestBuilder.RemoveAccents(req.Contractor.City)),
                new XElement("ESTADO", req.Contractor.StateCode),
                new XElement("CODIGO_POSTAL", req.Contractor.PostalCode),
                new XElement("PAIS_DOMICILIO", "MEX")),
            new XElement("TELEFONOS",
                new XElement("TELEFONO",
                    new XElement("CVE_LADA", lada),
                    new XElement("CVE_LADA_NACIONAL", lada),
                    new XElement("NUMERO_TELEFONO", numero))),
            new XElement("CORREOS",
                new XElement("CORREO",
                    new XElement("CORREO_ELECTRONICO", req.Contractor.Email))));
    }

    private static XElement BuildConductor(InsurerEmitRequest req) =>
        new("CONDUCTOR",
            new XElement("RFC", req.HabitualDriver.Rfc),
            new XElement("NOMBRES", GnpRequestBuilder.RemoveAccents(req.HabitualDriver.FirstName)),
            new XElement("APELLIDO_PATERNO", GnpRequestBuilder.RemoveAccents(req.HabitualDriver.LastNamePaternal)),
            new XElement("APELLIDO_MATERNO", GnpRequestBuilder.RemoveAccents(req.HabitualDriver.LastNameMaternal)),
            new XElement("SEXO", "M"),
            new XElement("ESTADO_CIVIL", "S"));

    private static XElement BuildBeneficiarios() =>
        new("BENEFICIARIOS",
            new XElement("BENEFICIARIO",
                new XElement("BAN_IRREVOCABLE", string.Empty),
                new XElement("NOMBRES", string.Empty),
                new XElement("APELLIDO_PATERNO", " "),
                new XElement("APELLIDO_MATERNO", " "),
                new XElement("PCT_BENEFICIO", " "),
                new XElement("TIPO_PERSONA", " ")));

    private static string FormatMoney(decimal amount) =>
        amount.ToString("0.##", CultureInfo.InvariantCulture);
}
