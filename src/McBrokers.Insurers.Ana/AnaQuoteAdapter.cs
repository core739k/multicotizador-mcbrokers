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

    public AnaQuoteAdapter(HttpClient http, ILogger<AnaQuoteAdapter> logger, TimeProvider time)
    {
        _http = http;
        _logger = logger;
        _time = time;
    }

    public InsurerCode Code => InsurerCode.Ana;

    public async Task<InsurerQuoteOutcome> QuoteAsync(InsurerQuoteRequest request, CancellationToken cancellationToken)
    {
        // ANA requiere EdoMun (5 dígitos = estado 2 + municipio 3); aquí se infiere del CP "9 002" → "09002".
        // En F4 entregable es estructura; el CP→edoMun real se resuelve en F4.5 cuando se conecte a ColoniaxCP.
        // Para fallback estructural usamos los primeros 5 dígitos del CP como placeholder.
        var edoMun = (request.PostalCode ?? string.Empty).PadLeft(5, '0');

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
