using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.Insurers.Abstractions.Soap;
using McBrokers.Insurers.Ana.Mapping;
using Microsoft.Extensions.Logging;

namespace McBrokers.Insurers.Ana;

public sealed class AnaQuoteAdapter : IInsurerAdapter
{
    private readonly HttpClient _http;
    private readonly ILogger<AnaQuoteAdapter> _logger;
    private readonly TimeProvider _time;
    private readonly IAnaPostalCodeResolver _postalResolver;

    public AnaQuoteAdapter(
        HttpClient http,
        ILogger<AnaQuoteAdapter> logger,
        TimeProvider time,
        IAnaPostalCodeResolver postalResolver)
    {
        _http = http;
        _logger = logger;
        _time = time;
        _postalResolver = postalResolver;
    }

    public InsurerCode Code => InsurerCode.Ana;

    public Task<InsurerEmitOutcome> EmitAsync(InsurerEmitRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<InsurerEmitOutcome>(new InsurerEmitOutcome.Failure(new InsurerEmitError(
            Domain.Quotations.ErrorCategory.Technical,
            "NOT_IMPLEMENTED",
            "Emisión ANA pendiente (TransaccionAsync con tipotransaccion=E).",
            LatencyMs: 0, RawRequest: null, RawResponse: null)));

    public async Task<InsurerQuoteOutcome> QuoteAsync(InsurerQuoteRequest request, CancellationToken cancellationToken)
    {
        // Resuelve EdoMun real desde ANA ColoniaxCP (con cache 24h). Si el servicio falla, fallback al CP.
        var postal = await _postalResolver
            .ResolveAsync(request.PostalCode, request.Credentials, request.EnvironmentConfig.EndpointUrl, cancellationToken)
            .ConfigureAwait(false);
        var edoMun = postal.EdoMun;

        var inner = AnaRequestBuilder.BuildTransaccionesXml(request, edoMun);
        var soapXml = AnaRequestBuilder.BuildSoapEnvelope(request, inner);

        using var content = new StringContent(soapXml, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue(SoapEnvelope.MediaType(SoapVersion.Soap11))
        {
            CharSet = "utf-8",
        };

        using var http = new HttpRequestMessage(HttpMethod.Post, request.EnvironmentConfig.EndpointUrl)
        {
            Content = content,
        };
        http.Headers.TryAddWithoutValidation("X-Correlation-Id", request.CorrelationId);
        http.Headers.TryAddWithoutValidation("SOAPAction", "\"http://server.anaseguros.com.mx/WSCOR/Transaccion\"");

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
                _logger.LogWarning("ANA returned HTTP {Status}", (int)response.StatusCode);
                return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                    QuotationInsurerStatus.InsurerDown, ErrorCategory.InsurerDown,
                    $"HTTP_{(int)response.StatusCode}",
                    $"ANA respondió HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
                    (int)sw.ElapsedMilliseconds, soapXml, responseXml));
            }

            return AnaResponseParser.Parse(soapXml, responseXml, (int)sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.Timeout, ErrorCategory.InsurerDown,
                "TIMEOUT", $"ANA no respondió en {request.EnvironmentConfig.TimeoutSeconds}s.",
                (int)sw.ElapsedMilliseconds, soapXml, null));
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            return new InsurerQuoteOutcome.Failure(new InsurerErrorResponse(
                QuotationInsurerStatus.InsurerDown, ErrorCategory.InsurerDown,
                "HTTP_ERROR", $"Error de red contra ANA: {ex.Message}",
                (int)sw.ElapsedMilliseconds, soapXml, null));
        }
    }
}
