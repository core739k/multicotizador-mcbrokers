using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.Insurers.Gnp.Mapping;
using Microsoft.Extensions.Logging;

namespace McBrokers.Insurers.Gnp;

public sealed class GnpQuoteAdapter : IInsurerAdapter
{
    private readonly HttpClient _http;
    private readonly ILogger<GnpQuoteAdapter> _logger;
    private readonly TimeProvider _time;

    public GnpQuoteAdapter(HttpClient http, ILogger<GnpQuoteAdapter> logger, TimeProvider time)
    {
        _http = http;
        _logger = logger;
        _time = time;
    }

    public InsurerCode Code => InsurerCode.Gnp;

    public async Task<InsurerQuoteOutcome> QuoteAsync(
        InsurerQuoteRequest request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        var requestXml = GnpRequestBuilder.BuildQuoteRequest(request, today);

        using var content = new StringContent(requestXml, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/xml");

        using var http = new HttpRequestMessage(HttpMethod.Post, request.EnvironmentConfig.EndpointUrl)
        {
            Content = content,
        };
        http.Headers.TryAddWithoutValidation("X-Correlation-Id", request.CorrelationId);

        var sw = Stopwatch.StartNew();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(request.EnvironmentConfig.TimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

            using var response = await _http.SendAsync(http, linked.Token).ConfigureAwait(false);
            var responseXml = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "GNP returned HTTP {Status} for correlation {CorrelationId}",
                    (int)response.StatusCode, request.CorrelationId);

                return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                    Status: response.StatusCode is System.Net.HttpStatusCode.RequestTimeout
                        ? QuotationInsurerStatus.Timeout
                        : QuotationInsurerStatus.InsurerDown,
                    Category: ErrorCategory.InsurerDown,
                    ExternalCode: $"HTTP_{(int)response.StatusCode}",
                    ExternalMessage: $"GNP respondió HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
                    LatencyMs: (int)sw.ElapsedMilliseconds,
                    RawRequest: requestXml,
                    RawResponse: responseXml));
            }

            return GnpResponseParser.Parse(requestXml, responseXml, (int)sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.Timeout, ErrorCategory.InsurerDown,
                "TIMEOUT", $"GNP no respondió en {request.EnvironmentConfig.TimeoutSeconds}s.",
                (int)sw.ElapsedMilliseconds, requestXml, null));
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.InsurerDown, ErrorCategory.InsurerDown,
                "HTTP_ERROR", $"Error de red contra GNP: {ex.Message}",
                (int)sw.ElapsedMilliseconds, requestXml, null));
        }
    }
}
