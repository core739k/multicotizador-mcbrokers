using System.Globalization;
using System.Xml.Linq;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;

namespace McBrokers.Insurers.Gnp.Mapping;

public static class GnpResponseParser
{
    public static InsurerQuoteOutcome Parse(string rawRequest, string rawResponse, int latencyMs)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(rawResponse);
        }
        catch (System.Xml.XmlException ex)
        {
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.Failed, ErrorCategory.Technical,
                "PARSE_ERROR", $"Respuesta GNP no es XML válido: {ex.Message}",
                latencyMs, rawRequest, rawResponse));
        }

        var error = TryReadError(doc);
        if (error is not null)
        {
            var category = error.Code switch
            {
                "0288" => ErrorCategory.Business,
                _ when error.Code.StartsWith("02", StringComparison.Ordinal) => ErrorCategory.Business,
                _ => ErrorCategory.Technical,
            };
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.Failed, category,
                error.Code, error.Message, latencyMs, rawRequest, rawResponse));
        }

        var concepts = doc.Descendants("CONCEPTO_ECONOMICO")
            .Concat(doc.Descendants("conceptoEconomico"))
            .ToList();

        var total = ConceptValue(concepts, "TOTAL_PAGAR");
        var net = ConceptValue(concepts, "PRIMA_NETA");
        var tax = ConceptValue(concepts, "IVA");
        var fees = ConceptValue(concepts, "DERECHOS_POLIZA");

        if (total is null || net is null)
        {
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.Failed, ErrorCategory.Technical,
                "MISSING_AMOUNTS",
                "La respuesta de GNP no contiene los conceptos económicos esperados (TOTAL_PAGAR / PRIMA_NETA).",
                latencyMs, rawRequest, rawResponse));
        }

        var numCotizacion = doc.Descendants("NUM_COTIZACION").FirstOrDefault()?.Value?.Trim() ?? string.Empty;

        return new InsurerQuoteOutcome.Success(new InsurerQuoteResponse(
            ExternalQuoteRef: numCotizacion,
            PremiumTotal: total.Value,
            PremiumNet: net.Value,
            Tax: tax ?? 0m,
            Fees: fees ?? 0m,
            LatencyMs: latencyMs,
            RawRequest: rawRequest,
            RawResponse: rawResponse));
    }

    private static decimal? ConceptValue(List<XElement> concepts, string name) =>
        concepts
            .Where(c => string.Equals(
                c.Element("NOMBRE")?.Value?.Trim() ?? c.Element("nombre")?.Value?.Trim(),
                name, StringComparison.OrdinalIgnoreCase))
            .Select(c => ParseDecimal(c.Element("MONTO")?.Value ?? c.Element("monto")?.Value))
            .FirstOrDefault(v => v.HasValue);

    private static decimal? ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static GnpError? TryReadError(XDocument doc)
    {
        var errEl = doc.Descendants("ERROR").FirstOrDefault()
            ?? doc.Descendants("error").FirstOrDefault();
        if (errEl is null) return null;

        var code = errEl.Element("CODIGO")?.Value?.Trim()
                ?? errEl.Element("codigo")?.Value?.Trim()
                ?? errEl.Attribute("codigo")?.Value
                ?? "UNKNOWN";

        var message = errEl.Element("MENSAJE")?.Value?.Trim()
                   ?? errEl.Element("mensaje")?.Value?.Trim()
                   ?? errEl.Value?.Trim()
                   ?? string.Empty;

        if (string.IsNullOrWhiteSpace(message)) return null;

        return new GnpError(code, message);
    }

    private sealed record GnpError(string Code, string Message);
}
