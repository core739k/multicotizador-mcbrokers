using System.Xml.Linq;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;

namespace McBrokers.Insurers.Gnp.Mapping;

public static class GnpEmissionParser
{
    public static InsurerEmitOutcome Parse(string rawRequest, string rawResponse, int latencyMs)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(rawResponse);
        }
        catch (System.Xml.XmlException ex)
        {
            return new InsurerEmitOutcome.Failure(new InsurerEmitError(
                ErrorCategory.Technical, "PARSE_ERROR",
                $"Respuesta de emisión GNP no es XML válido: {ex.Message}",
                latencyMs, rawRequest, rawResponse));
        }

        var policyNumber = doc.Descendants("NUM_POLIZA").FirstOrDefault()?.Value?.Trim();
        var errorEl = doc.Descendants("ERROR").FirstOrDefault();

        if (errorEl is not null)
        {
            var code = errorEl.Element("CODIGO")?.Value?.Trim() ?? "EMIT_ERROR";
            var message = errorEl.Element("MENSAJE")?.Value?.Trim() ?? errorEl.Value;
            return new InsurerEmitOutcome.Failure(new InsurerEmitError(
                ErrorCategory.Business, code, message,
                latencyMs, rawRequest, rawResponse));
        }

        if (string.IsNullOrWhiteSpace(policyNumber))
        {
            return new InsurerEmitOutcome.Failure(new InsurerEmitError(
                ErrorCategory.Technical, "MISSING_POLICY_NUMBER",
                "GNP no devolvió NUM_POLIZA en la respuesta de emisión.",
                latencyMs, rawRequest, rawResponse));
        }

        // El PDF se obtiene con un segundo llamado a buscarPoliza; el adapter ejecuta ese paso.
        return new InsurerEmitOutcome.Success(new InsurerEmitResponse(
            PolicyNumber: policyNumber!,
            PdfDownloadUrl: null,
            LatencyMs: latencyMs,
            RawRequest: rawRequest,
            RawResponse: rawResponse));
    }

    public static string? ParsePdfUrlFromPrintResponse(string rawJson)
    {
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(rawJson);
            if (json.RootElement.TryGetProperty("RESULTADO", out var resultado))
            {
                if (resultado.TryGetProperty("URL_DOCUMENTO", out var url))
                {
                    return url.GetString();
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
        return null;
    }
}
