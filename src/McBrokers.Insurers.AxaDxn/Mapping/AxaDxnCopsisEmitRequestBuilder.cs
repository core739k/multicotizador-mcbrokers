using System.Xml.Linq;
using McBrokers.Insurers.Abstractions;

namespace McBrokers.Insurers.AxaDxn.Mapping;

/// <summary>
/// Construye el XML SOLICITUDEMISION que va dentro del campo "v1" del body JSON enviado
/// a COPSIS (https://lb1.copsis.com/sio4apolizas-lazy-fetch/EmisionIncisoAxaAPI).
///
/// Estructura replica el legacy (CotizacionNegocio.cs:5333-5382):
///   SOLICITUDEMISION
///     clientews=275
///     CotizaAutoRespuesta/CotizarIncisoResponse → XML crudo de la respuesta de cotización
///       previa, sin el wrapper soapenv:Envelope/Body.
///     datosEmision/datosContratante → datos del tomador
///     datosEmision/datosVehiculo → datos del vehículo
/// </summary>
public static class AxaDxnCopsisEmitRequestBuilder
{
    // El legacy embebe clientews=275 hardcoded. Es el código de cliente AXA del corredor
    // (MCBrokers) en la integración COPSIS — no es per-emisión.
    private const string ClientewsCode = "275";

    public static string BuildSolicitudEmisionXml(
        InsurerEmitRequest request, AxaDxnAdapterConfig axa)
    {
        var contractor = request.Contractor;
        var vehicle = request.Vehicle;

        var tipoValor = AxaDxnRequestBuilder.MapValuationDescriptor(request.Valuation);
        var porcentajeValor = AxaDxnRequestBuilder.MapValuationPercentage(request.Valuation);

        var datosContratante = new XElement("datosContratante",
            new XElement("numeroPoliza", axa.PolizaAutos ?? string.Empty),
            new XElement("tipoValor", tipoValor),
            new XElement("porcentajeValor", porcentajeValor),
            new XElement("direccion", $"{contractor.Street} {contractor.ExteriorNumber}".Trim()),
            new XElement("colonia", contractor.Neighborhood),
            new XElement("cp", contractor.PostalCode),
            new XElement("edo", contractor.StateCode),
            new XElement("mun", contractor.City),
            new XElement("nombreConductor", contractor.FirstName),
            new XElement("paterno", contractor.LastNamePaternal),
            new XElement("materno", contractor.LastNameMaternal),
            new XElement("vendedor", request.AgentExternalCode ?? string.Empty),
            new XElement("benefAcc", string.Empty),
            new XElement("benefAccPorc", "100"),
            new XElement("nomina", string.Empty),
            new XElement("rfc", contractor.Rfc),
            new XElement("identificador", "9999"),
            new XElement("tel", contractor.Phone),
            new XElement("beneficiario", string.Empty),
            new XElement("clabe", string.Empty),
            new XElement("tarjeta", string.Empty),
            new XElement("datosFiscales",
                new XElement("enviar", "0"),
                new XElement("tipo", "1"),
                new XElement("regCapital", string.Empty),
                new XElement("regFiscal", string.Empty),
                new XElement("usoCFDI", string.Empty)));

        var datosVehiculo = new XElement("datosVehiculo",
            new XElement("noEconomico", "0"),
            new XElement("serie", vehicle.SerialNumber),
            new XElement("placas", vehicle.Plate),
            new XElement("estadoPlacas", contractor.StateCode),
            new XElement("motor", vehicle.EngineNumber),
            new XElement("valorUnidad", "0"),
            new XElement("equipoEspecial", string.Empty));

        var cotizaAutoRespuesta = BuildCotizaAutoRespuesta(request.RawQuoteResponseXml);

        var root = new XElement("SOLICITUDEMISION",
            new XElement("clientews", ClientewsCode),
            cotizaAutoRespuesta,
            new XElement("datosEmision", datosContratante, datosVehiculo));

        return root.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement BuildCotizaAutoRespuesta(string? rawQuoteResponseXml)
    {
        // CotizaAutoRespuesta/CotizarIncisoResponse contiene el body de la respuesta de
        // cotización previa SIN el wrapper soapenv. Si no tenemos XML, dejamos el nodo vacío
        // (COPSIS seguramente lo rechazará pero el contrato del v1 sigue siendo válido).
        if (string.IsNullOrWhiteSpace(rawQuoteResponseXml))
        {
            return new XElement("CotizaAutoRespuesta",
                new XElement("CotizarIncisoResponse"));
        }

        var body = StripSoapWrappers(rawQuoteResponseXml);

        // CotizarIncisoResponse vive como cadena cruda dentro del CotizaAutoRespuesta —
        // mismo abordaje del legacy (interpolación de string en lugar de XElement nested).
        // XElement con XText preservaría angle brackets pero los escaparía; necesitamos
        // que vayan literales. Usamos XCData para garantizar que se serialice como-está,
        // pero el legacy NO envuelve en CDATA; lo deja como markup directo. Para imitar
        // exactamente, retornamos el contenedor parseando una cadena concatenada.
        var inlined = $"<CotizaAutoRespuesta><CotizarIncisoResponse>{body}</CotizarIncisoResponse></CotizaAutoRespuesta>";
        try
        {
            return XElement.Parse(inlined);
        }
        catch (System.Xml.XmlException)
        {
            // Defensivo: si el body crudo no es XML parseable como fragmento, devolvemos vacío.
            return new XElement("CotizaAutoRespuesta",
                new XElement("CotizarIncisoResponse"));
        }
    }

    private static string StripSoapWrappers(string xml)
    {
        // El legacy hace 3 replaces literales para quitar prólogo XML + wrapper soapenv.
        // Replicamos lo mismo — más robusto sería parsear y extraer el contenido, pero el
        // body crudo de AXA viene con prefijos namespace (ns2:) que cambian al rehidratar
        // y romperían lo que COPSIS espera. Sólo quitamos los wrappers, dejamos el resto literal.
        return xml
            .Replace("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", string.Empty)
            .Replace("<?xml version=\"1.0\" encoding=\"utf-8\"?>", string.Empty)
            .Replace("<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"><soapenv:Header/><soapenv:Body><ns2:CotizarIncisoResponse xmlns:ns2=\"http://axa.com.mx/autos/flotillas/ws\">", string.Empty)
            .Replace("</ns2:CotizarIncisoResponse></soapenv:Body></soapenv:Envelope>", string.Empty)
            .Trim();
    }
}
