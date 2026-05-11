using System.Globalization;
using System.Xml.Linq;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;

namespace McBrokers.Insurers.AxaDxn.Mapping;

public static class AxaDxnResponseParser
{
    public static InsurerQuoteOutcome Parse(string rawRequest, string rawResponse, int latencyMs)
    {
        XDocument doc;
        try { doc = XDocument.Parse(rawResponse); }
        catch (System.Xml.XmlException ex)
        {
            return Fail("PARSE_ERROR", $"Respuesta AXA DXN no es XML válido: {ex.Message}", ErrorCategory.Technical);
        }

        var error = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "errorCotizacion")
                 ?? doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "error");
        if (error is not null && !string.IsNullOrWhiteSpace(error.Value))
        {
            var code = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "codigoError")?.Value
                    ?? error.Attribute("codigo")?.Value ?? "UNKNOWN";
            return Fail(code, error.Value, ErrorCategory.Business);
        }

        var primaNeta = ReadDecimal(doc, "primaNeta");
        var primaTotal = ReadDecimal(doc, "primaTotal");
        var iva = ReadDecimal(doc, "iva") ?? 0m;
        var fees = ReadDecimal(doc, "derechos") ?? 0m;
        var folio = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "numeroCotizacion")?.Value
                 ?? doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "folio")?.Value
                 ?? string.Empty;

        if (primaTotal is null || primaNeta is null)
        {
            return Fail("MISSING_AMOUNTS", "AXA DXN no devolvió primaTotal o primaNeta.", ErrorCategory.Technical);
        }

        return new InsurerQuoteOutcome.Success(new InsurerQuoteResponse(
            ExternalQuoteRef: folio,
            PremiumTotal: primaTotal.Value,
            PremiumNet: primaNeta.Value,
            Tax: iva,
            Fees: fees,
            LatencyMs: latencyMs,
            RawRequest: rawRequest,
            RawResponse: rawResponse));

        InsurerQuoteOutcome Fail(string code, string msg, ErrorCategory cat) =>
            new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.Failed, cat, code, msg,
                latencyMs, rawRequest, rawResponse));
    }

    private static decimal? ReadDecimal(XDocument doc, string elementName)
    {
        var raw = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == elementName)?.Value;
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
