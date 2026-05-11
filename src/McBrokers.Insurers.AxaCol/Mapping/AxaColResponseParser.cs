using System.Globalization;
using System.Xml.Linq;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;

namespace McBrokers.Insurers.AxaCol.Mapping;

public static class AxaColResponseParser
{
    public static InsurerQuoteOutcome Parse(string rawRequest, string rawResponse, int latencyMs)
    {
        XDocument doc;
        try { doc = XDocument.Parse(rawResponse); }
        catch (System.Xml.XmlException ex)
        {
            return Fail("PARSE_ERROR", $"Respuesta AXA COL no es XML válido: {ex.Message}", ErrorCategory.Technical);
        }

        // El response también devuelve un string XML embebido en createSolicitudPolizasInmediataResponse/return.
        var ret = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName is "return" or "createSolicitudPolizasInmediataReturn");

        if (ret is null || string.IsNullOrWhiteSpace(ret.Value))
        {
            return Fail("EMPTY_RESULT", "AXA COL respondió sin <return>.", ErrorCategory.Technical);
        }

        XDocument inner;
        try { inner = XDocument.Parse(ret.Value); }
        catch (System.Xml.XmlException ex)
        {
            return Fail("PARSE_ERROR_INNER", $"XML interno AXA COL no se pudo parsear: {ex.Message}", ErrorCategory.Technical);
        }

        var error = inner.Descendants("Error").FirstOrDefault()
                 ?? inner.Descendants("error").FirstOrDefault();
        if (error is not null && !string.IsNullOrWhiteSpace(error.Value))
        {
            var code = error.Element("Codigo")?.Value ?? error.Attribute("codigo")?.Value ?? "UNKNOWN";
            var msg = error.Element("Mensaje")?.Value ?? error.Value;
            return Fail(code, msg, ErrorCategory.Business);
        }

        var primaNeta = ReadDecimal(inner, "PrimaNeta");
        var primaTotal = ReadDecimal(inner, "PrimaTotal");
        var iva = ReadDecimal(inner, "IVA") ?? 0m;
        var derechos = ReadDecimal(inner, "Derechos") ?? 0m;
        var folio = inner.Descendants("FolioCotizacion").FirstOrDefault()?.Value
                 ?? inner.Descendants("NumeroCotizacion").FirstOrDefault()?.Value
                 ?? string.Empty;

        if (primaTotal is null || primaNeta is null)
        {
            return Fail("MISSING_AMOUNTS", "AXA COL no devolvió PrimaTotal o PrimaNeta.", ErrorCategory.Technical);
        }

        return new InsurerQuoteOutcome.Success(new InsurerQuoteResponse(
            ExternalQuoteRef: folio,
            PremiumTotal: primaTotal.Value,
            PremiumNet: primaNeta.Value,
            Tax: iva,
            Fees: derechos,
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
        var raw = doc.Descendants(elementName).FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
