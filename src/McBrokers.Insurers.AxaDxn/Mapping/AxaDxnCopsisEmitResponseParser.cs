using System.Xml;
using System.Xml.Linq;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;

namespace McBrokers.Insurers.AxaDxn.Mapping;

/// <summary>
/// Parser de la respuesta COPSIS para emisión AXA DXN. COPSIS devuelve SOAP+CDATA con
/// XML interno (NO JSON, como sugería el adapter inicial). Estructura:
///   /soap:Envelope/soap:Body/tempuri:EmiteAxaResponse/tempuri:EmiteAxaResult
///     CDATA → &lt;RESPUESTA&gt;&lt;url&gt;...&lt;/url&gt;&lt;inciso&gt;...&lt;/inciso&gt;&lt;/RESPUESTA&gt;
/// El legacy (CotizacionNegocio.cs:5407-5440) detecta error con substring "error" en el
/// CDATA y en éxito lee &lt;url&gt; (PDF) e &lt;inciso&gt; (folio). Replicamos esa heurística.
/// </summary>
public static class AxaDxnCopsisEmitResponseParser
{
    private const string SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";
    private const string TempuriNs = "http://tempuri.org/";

    public static InsurerEmitOutcome Parse(string rawRequest, string rawResponse, int latencyMs)
    {
        XmlDocument xmlDoc;
        try
        {
            xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(rawResponse);
        }
        catch (XmlException ex)
        {
            return Fail("PARSE_ERROR",
                $"Respuesta COPSIS no es XML válido: {ex.Message}",
                ErrorCategory.Technical, latencyMs, rawRequest, rawResponse);
        }

        var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
        nsmgr.AddNamespace("soap", SoapNs);
        nsmgr.AddNamespace("tempuri", TempuriNs);

        // Mismo XPath que el legacy: localiza el nodo EmiteAxaResult dentro del SOAP envelope.
        var resultNode = xmlDoc.SelectSingleNode(
            "//soap:Body/tempuri:EmiteAxaResponse/tempuri:EmiteAxaResult", nsmgr);
        if (resultNode is null)
        {
            return Fail("PARSE_ERROR",
                "Respuesta COPSIS no contiene //soap:Body/EmiteAxaResponse/EmiteAxaResult.",
                ErrorCategory.Technical, latencyMs, rawRequest, rawResponse);
        }

        // Aceptamos CDATA o texto plano — defensivo por si COPSIS cambia el wrapping.
        var cdataContent = ExtractInnerCdataOrText(resultNode);
        if (string.IsNullOrWhiteSpace(cdataContent))
        {
            return Fail("PARSE_ERROR",
                "EmiteAxaResult está vacío.",
                ErrorCategory.Technical, latencyMs, rawRequest, rawResponse);
        }

        // Heurística legacy: si el CDATA menciona "error" en cualquier parte, lo trataos
        // como rechazo de negocio. Frágil pero replica comportamiento productivo.
        if (cdataContent.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            var msg = ExtractErrorMessage(cdataContent) ?? cdataContent.Trim();
            return Fail("COPSIS_ERROR", msg, ErrorCategory.Business,
                latencyMs, rawRequest, rawResponse);
        }

        XDocument respuesta;
        try
        {
            respuesta = XDocument.Parse(cdataContent);
        }
        catch (XmlException ex)
        {
            return Fail("PARSE_ERROR",
                $"CDATA interno no es XML válido: {ex.Message}",
                ErrorCategory.Technical, latencyMs, rawRequest, rawResponse);
        }

        var url = respuesta.Descendants("url").FirstOrDefault()?.Value?.Trim();
        var inciso = respuesta.Descendants("inciso").FirstOrDefault()?.Value?.Trim();

        if (string.IsNullOrWhiteSpace(inciso))
        {
            return Fail("MISSING_POLIZA",
                "COPSIS no devolvió <inciso> — respuesta incompleta o llaves inválidas (dummy).",
                ErrorCategory.Technical, latencyMs, rawRequest, rawResponse);
        }

        return new InsurerEmitOutcome.Success(new InsurerEmitResponse(
            PolicyNumber: inciso!,
            PdfDownloadUrl: string.IsNullOrWhiteSpace(url) ? null : url,
            LatencyMs: latencyMs,
            RawRequest: rawRequest,
            RawResponse: rawResponse));
    }

    private static string? ExtractInnerCdataOrText(XmlNode resultNode)
    {
        // El legacy itera buscando un CDataSection — replicamos. Si no hay CDATA, devolvemos
        // el InnerText (caso degenerado pero parseable).
        foreach (XmlNode child in resultNode.ChildNodes)
        {
            if (child is XmlCDataSection cdata) return cdata.InnerText;
        }
        return resultNode.InnerText;
    }

    private static string? ExtractErrorMessage(string cdataContent)
    {
        // Intento extraer <error>...</error> si existe; si no, devolvemos null y el caller
        // usa el CDATA completo como mensaje (con trim).
        try
        {
            var doc = XDocument.Parse(cdataContent);
            var errorNode = doc.Descendants()
                .FirstOrDefault(e => string.Equals(e.Name.LocalName, "error", StringComparison.OrdinalIgnoreCase));
            return errorNode?.Value?.Trim();
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static InsurerEmitOutcome Fail(
        string code, string message, ErrorCategory category,
        int latencyMs, string? rawRequest, string? rawResponse) =>
        new InsurerEmitOutcome.Failure(new InsurerEmitError(
            category, code, message, latencyMs, rawRequest, rawResponse));
}
