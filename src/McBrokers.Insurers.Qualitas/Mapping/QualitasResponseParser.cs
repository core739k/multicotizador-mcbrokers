using System.Globalization;
using System.Xml.Linq;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.Insurers.Abstractions.Soap;

namespace McBrokers.Insurers.Qualitas.Mapping;

public static class QualitasResponseParser
{
    public static InsurerQuoteOutcome Parse(string rawRequest, string rawSoapResponse, int latencyMs)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(rawSoapResponse);
        }
        catch (System.Xml.XmlException ex)
        {
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.Failed, ErrorCategory.Technical,
                "PARSE_ERROR", $"Respuesta Quálitas no es XML válido: {ex.Message}",
                latencyMs, rawRequest, rawSoapResponse));
        }

        // Desempacar el body — obtenerNuevaEmisionResponse contiene obtenerNuevaEmisionResult con el XML de Movimientos como string.
        var result = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "obtenerNuevaEmisionResult");

        if (result is null || string.IsNullOrWhiteSpace(result.Value))
        {
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.Failed, ErrorCategory.Technical,
                "EMPTY_RESULT", "Quálitas devolvió un Body sin obtenerNuevaEmisionResult.",
                latencyMs, rawRequest, rawSoapResponse));
        }

        XDocument inner;
        try
        {
            inner = XDocument.Parse(result.Value);
        }
        catch (System.Xml.XmlException ex)
        {
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.Failed, ErrorCategory.Technical,
                "PARSE_ERROR_INNER", $"El XML interno de Quálitas no se pudo parsear: {ex.Message}",
                latencyMs, rawRequest, rawSoapResponse));
        }

        var errorCode = inner.Descendants("CodigoError").FirstOrDefault()?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(errorCode) && errorCode != "0")
        {
            var category = errorCode.StartsWith("0288", StringComparison.Ordinal)
                ? ErrorCategory.Business
                : ErrorCategory.Technical;
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.Failed, category,
                errorCode, errorCode,
                latencyMs, rawRequest, rawSoapResponse));
        }

        var primas = inner.Descendants("Primas").FirstOrDefault();
        if (primas is null)
        {
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.Failed, ErrorCategory.Technical,
                "MISSING_PRIMAS", "Quálitas respondió sin bloque <Primas>.",
                latencyMs, rawRequest, rawSoapResponse));
        }

        var total = ParseDecimal(primas.Element("PrimaTotal")?.Value);
        var net = ParseDecimal(primas.Element("PrimaNeta")?.Value);
        var tax = ParseDecimal(primas.Element("Impuesto")?.Value);
        var fees = ParseDecimal(primas.Element("Derecho")?.Value);

        if (total is null || net is null)
        {
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.Failed, ErrorCategory.Technical,
                "MISSING_AMOUNTS", "Quálitas no devolvió PrimaTotal o PrimaNeta.",
                latencyMs, rawRequest, rawSoapResponse));
        }

        var noCotizacion = inner.Descendants("Movimiento").FirstOrDefault()?.Attribute("NoCotizacion")?.Value
                        ?? string.Empty;

        return new InsurerQuoteOutcome.Success(new InsurerQuoteResponse(
            ExternalQuoteRef: noCotizacion,
            PremiumTotal: total.Value,
            PremiumNet: net.Value,
            Tax: tax ?? 0m,
            Fees: fees ?? 0m,
            LatencyMs: latencyMs,
            RawRequest: rawRequest,
            RawResponse: rawSoapResponse));
    }

    private static decimal? ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
