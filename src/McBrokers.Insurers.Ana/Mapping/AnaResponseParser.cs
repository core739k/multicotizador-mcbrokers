using System.Globalization;
using System.Xml.Linq;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;

namespace McBrokers.Insurers.Ana.Mapping;

public static class AnaResponseParser
{
    public static InsurerQuoteOutcome Parse(string rawRequest, string rawResponse, int latencyMs)
    {
        XDocument doc;
        try { doc = XDocument.Parse(rawResponse); }
        catch (System.Xml.XmlException ex)
        {
            return Fail("PARSE_ERROR", $"Respuesta ANA no es XML válido: {ex.Message}", ErrorCategory.Technical);
        }

        // TransaccionResponse contiene TransaccionResult con el XML inner.
        var result = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName is "TransaccionResult");
        if (result is null || string.IsNullOrWhiteSpace(result.Value))
        {
            return Fail("EMPTY_RESULT", "ANA respondió sin TransaccionResult.", ErrorCategory.Technical);
        }

        XDocument inner;
        try { inner = XDocument.Parse(result.Value); }
        catch (System.Xml.XmlException ex)
        {
            return Fail("PARSE_ERROR_INNER", $"XML inner de ANA no se pudo parsear: {ex.Message}", ErrorCategory.Technical);
        }

        var error = inner.Descendants("error").FirstOrDefault();
        if (error is not null)
        {
            var code = error.Attribute("codigo")?.Value ?? "UNKNOWN";
            var message = error.Attribute("descripcion")?.Value ?? error.Value;
            return Fail(code, message, ErrorCategory.Business);
        }

        var poliza = inner.Descendants("poliza").FirstOrDefault();
        if (poliza is null)
        {
            return Fail("MISSING_POLIZA", "ANA respondió sin bloque <poliza>.", ErrorCategory.Technical);
        }

        var primaNeta = ReadDecimal(poliza, "prima_neta") ?? ReadDecimal(poliza, "primaNeta");
        var primaTotal = ReadDecimal(poliza, "prima_total") ?? ReadDecimal(poliza, "primaTotal");
        var iva = ReadDecimal(poliza, "iva") ?? 0m;
        var fees = ReadDecimal(poliza, "derechos") ?? 0m;
        var cotizacion = poliza.Attribute("cotizacion")?.Value
                      ?? inner.Descendants("transaccion").FirstOrDefault()?.Attribute("cotizacion")?.Value
                      ?? string.Empty;

        if (primaTotal is null || primaNeta is null)
        {
            return Fail("MISSING_AMOUNTS", "ANA no devolvió prima_total o prima_neta.", ErrorCategory.Technical);
        }

        return new InsurerQuoteOutcome.Success(new InsurerQuoteResponse(
            ExternalQuoteRef: cotizacion,
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

    private static decimal? ReadDecimal(XElement poliza, string attrName)
    {
        var raw = poliza.Attribute(attrName)?.Value;
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
