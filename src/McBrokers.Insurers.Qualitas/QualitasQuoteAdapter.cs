using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.Insurers.Abstractions.Soap;
using McBrokers.Insurers.Qualitas.Mapping;
using Microsoft.Extensions.Logging;

namespace McBrokers.Insurers.Qualitas;

public sealed class QualitasQuoteAdapter : IInsurerAdapter
{
    private readonly HttpClient _http;
    private readonly ILogger<QualitasQuoteAdapter> _logger;
    private readonly TimeProvider _time;

    public QualitasQuoteAdapter(HttpClient http, ILogger<QualitasQuoteAdapter> logger, TimeProvider time)
    {
        _http = http;
        _logger = logger;
        _time = time;
    }

    public InsurerCode Code => InsurerCode.Qua;

    public Task<InsurerEmitOutcome> EmitAsync(InsurerEmitRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<InsurerEmitOutcome>(new InsurerEmitOutcome.Failure(new InsurerEmitError(
            Domain.Quotations.ErrorCategory.Technical,
            "NOT_IMPLEMENTED",
            "Emisión Quálitas pendiente de implementar (TipoMovimiento=3 con datos completos del titular).",
            LatencyMs: 0, RawRequest: null, RawResponse: null)));

    public async Task<InsurerQuoteOutcome> QuoteAsync(InsurerQuoteRequest request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        var inner = QualitasRequestBuilder.BuildMovimientosXml(request, today);
        var soapXml = QualitasRequestBuilder.BuildSoapBody(inner);

        using var content = new StringContent(soapXml, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue(SoapEnvelope.MediaType(SoapVersion.Soap12))
        {
            CharSet = "utf-8",
        };

        using var http = new HttpRequestMessage(HttpMethod.Post, request.EnvironmentConfig.EndpointUrl)
        {
            Content = content,
        };
        http.Headers.TryAddWithoutValidation("X-Correlation-Id", request.CorrelationId);
        http.Headers.TryAddWithoutValidation("SOAPAction", "\"http://qbcenter.qualitas.com.mx/wsCotQua/obtenerNuevaEmision\"");

        var sw = Stopwatch.StartNew();
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(request.EnvironmentConfig.TimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using var response = await _http.SendAsync(http, linked.Token).ConfigureAwait(false);
            var responseXml = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Quálitas returned HTTP {Status} for correlation {CorrelationId}",
                    (int)response.StatusCode, request.CorrelationId);

                return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                    Status: response.StatusCode is System.Net.HttpStatusCode.RequestTimeout
                        ? QuotationInsurerStatus.Timeout
                        : QuotationInsurerStatus.InsurerDown,
                    Category: ErrorCategory.InsurerDown,
                    ExternalCode: $"HTTP_{(int)response.StatusCode}",
                    ExternalMessage: $"Quálitas respondió HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
                    LatencyMs: (int)sw.ElapsedMilliseconds,
                    RawRequest: soapXml,
                    RawResponse: responseXml));
            }

            return QualitasResponseParser.Parse(soapXml, responseXml, (int)sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.Timeout, ErrorCategory.InsurerDown,
                "TIMEOUT", $"Quálitas no respondió en {request.EnvironmentConfig.TimeoutSeconds}s.",
                (int)sw.ElapsedMilliseconds, soapXml, null));
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.InsurerDown, ErrorCategory.InsurerDown,
                "HTTP_ERROR", $"Error de red contra Quálitas: {ex.Message}",
                (int)sw.ElapsedMilliseconds, soapXml, null));
        }
    }
}
