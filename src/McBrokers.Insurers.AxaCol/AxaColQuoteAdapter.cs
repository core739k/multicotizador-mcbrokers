using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.Insurers.AxaCol.Mapping;
using Microsoft.Extensions.Logging;

namespace McBrokers.Insurers.AxaCol;

public sealed class AxaColQuoteAdapter : IInsurerAdapter
{
    private readonly HttpClient _http;
    private readonly ILogger<AxaColQuoteAdapter> _logger;
    private readonly TimeProvider _time;

    public AxaColQuoteAdapter(HttpClient http, ILogger<AxaColQuoteAdapter> logger, TimeProvider time)
    {
        _http = http;
        _logger = logger;
        _time = time;
    }

    public InsurerCode Code => InsurerCode.AxaCol;

    public async Task<InsurerQuoteOutcome> QuoteAsync(InsurerQuoteRequest request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        var inner = AxaColRequestBuilder.BuildSolicitudXml(request, today);
        var soapXml = AxaColRequestBuilder.BuildSoapEnvelope(inner);

        using var content = new StringContent(soapXml, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };

        using var http = new HttpRequestMessage(HttpMethod.Post, request.EnvironmentConfig.EndpointUrl)
        {
            Content = content,
        };
        http.Headers.TryAddWithoutValidation("X-Correlation-Id", request.CorrelationId);
        http.Headers.TryAddWithoutValidation("SOAPAction", "\"urn:createSolicitudPolizasInmediata\"");

        var basicAuth = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{request.Credentials.Username}:{request.Credentials.Password}"));
        http.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

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
                _logger.LogWarning("AXA COL returned HTTP {Status}", (int)response.StatusCode);
                return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                    QuotationInsurerStatus.InsurerDown, ErrorCategory.InsurerDown,
                    $"HTTP_{(int)response.StatusCode}",
                    $"AXA COL respondió HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
                    (int)sw.ElapsedMilliseconds, soapXml, responseXml));
            }

            return AxaColResponseParser.Parse(soapXml, responseXml, (int)sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.Timeout, ErrorCategory.InsurerDown,
                "TIMEOUT", $"AXA COL no respondió en {request.EnvironmentConfig.TimeoutSeconds}s.",
                (int)sw.ElapsedMilliseconds, soapXml, null));
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.InsurerDown, ErrorCategory.InsurerDown,
                "HTTP_ERROR", $"Error de red contra AXA COL: {ex.Message}",
                (int)sw.ElapsedMilliseconds, soapXml, null));
        }
    }
}
