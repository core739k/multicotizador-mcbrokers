using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.Insurers.AxaDxn.Mapping;
using Microsoft.Extensions.Logging;

namespace McBrokers.Insurers.AxaDxn;

public sealed class AxaDxnQuoteAdapter : IInsurerAdapter
{
    private readonly HttpClient _http;
    private readonly ILogger<AxaDxnQuoteAdapter> _logger;
    private readonly TimeProvider _time;
    private readonly AxaDxnEmissionExecutor _emission;

    public AxaDxnQuoteAdapter(
        HttpClient http,
        ILogger<AxaDxnQuoteAdapter> logger,
        TimeProvider time,
        AxaDxnEmissionExecutor emission)
    {
        _http = http;
        _logger = logger;
        _time = time;
        _emission = emission;
    }

    public InsurerCode Code => InsurerCode.AxaDxn;

    public Task<InsurerEmitOutcome> EmitAsync(InsurerEmitRequest request, CancellationToken cancellationToken) =>
        _emission.EmitAsync(request, cancellationToken);

    public async Task<InsurerQuoteOutcome> QuoteAsync(InsurerQuoteRequest request, CancellationToken cancellationToken)
    {
        // AXA DXN exige AxaDxnAdapterConfig (Opción A — discriminated union por aseguradora).
        // Si no llega, es un bug del worker: signo de que ProcessQuotation no resolvió la
        // typed-config desde IAxaDxnConfigRepository.
        if (request.BusinessConfig is not AxaDxnAdapterConfig axa)
        {
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.Failed, ErrorCategory.Technical,
                "NO_CONFIG",
                "AXA DXN no recibió AxaDxnAdapterConfig — falta configuración admin o bug en worker.",
                LatencyMs: 0, RawRequest: null, RawResponse: null));
        }

        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        var soapXml = AxaDxnRequestBuilder.BuildSoapEnvelope(request, axa, today);

        using var content = new StringContent(soapXml, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };

        using var http = new HttpRequestMessage(HttpMethod.Post, request.Connection.EndpointUrl)
        {
            Content = content,
        };
        http.Headers.TryAddWithoutValidation("X-Correlation-Id", request.CorrelationId);
        http.Headers.TryAddWithoutValidation("SOAPAction", "\"urn:CotizarInciso\"");

        var basicAuth = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{axa.Usuario}:{axa.Password}"));
        http.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

        var sw = Stopwatch.StartNew();
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(request.Connection.TimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using var response = await _http.SendAsync(http, linked.Token).ConfigureAwait(false);
            var responseXml = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AXA DXN returned HTTP {Status}", (int)response.StatusCode);
                return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                    QuotationInsurerStatus.InsurerDown, ErrorCategory.InsurerDown,
                    $"HTTP_{(int)response.StatusCode}",
                    $"AXA DXN respondió HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
                    (int)sw.ElapsedMilliseconds, soapXml, responseXml));
            }

            return AxaDxnResponseParser.Parse(soapXml, responseXml, (int)sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.Timeout, ErrorCategory.InsurerDown,
                "TIMEOUT", $"AXA DXN no respondió en {request.Connection.TimeoutSeconds}s.",
                (int)sw.ElapsedMilliseconds, soapXml, null));
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.InsurerDown, ErrorCategory.InsurerDown,
                "HTTP_ERROR", $"Error de red contra AXA DXN: {ex.Message}",
                (int)sw.ElapsedMilliseconds, soapXml, null));
        }
    }
}
