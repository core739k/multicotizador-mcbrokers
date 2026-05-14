using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using Microsoft.Extensions.Logging;

namespace McBrokers.Insurers.AxaDxn.Mapping;

/// <summary>
/// Emisión AXA DXN vía COPSIS — intermediario externo que recibe los datos de la cotización
/// AXA, completa la emisión contra AXA, y devuelve el folio de póliza emitida.
/// Endpoint: https://lb1.copsis.com/sio4apolizas-lazy-fetch/EmisionIncisoAxaAPI
/// Auth: parámetros URL d4_key y b (no Basic Auth, distinto a cotización).
/// Content-Type: application/json (no XML).
/// </summary>
public sealed class AxaDxnEmissionExecutor
{
    // d4_key y b vienen del AxaDxnConfig (BD, cifrados con DataProtection). La URL base
    // y el timeout siguen como constantes — son estables del contrato COPSIS.
    private const string CopsisBaseUrl = "https://lb1.copsis.com/sio4apolizas-lazy-fetch/EmisionIncisoAxaAPI";
    private const int CopsisTimeoutSeconds = 50;

    private readonly HttpClient _http;
    private readonly ILogger<AxaDxnEmissionExecutor> _logger;

    public AxaDxnEmissionExecutor(HttpClient http, ILogger<AxaDxnEmissionExecutor> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<InsurerEmitOutcome> EmitAsync(InsurerEmitRequest request, CancellationToken cancellationToken)
    {
        if (request.BusinessConfig is not AxaDxnAdapterConfig axa)
        {
            return Fail("NO_CONFIG",
                "Emisión AXA DXN no recibió AxaDxnAdapterConfig — falta configuración admin.",
                ErrorCategory.Technical, latencyMs: 0, rawRequest: null, rawResponse: null);
        }

        var url = $"{CopsisBaseUrl}?d4_key={Uri.EscapeDataString(axa.CopsisD4Key)}&b={Uri.EscapeDataString(axa.CopsisB)}";

        // URL enmascarada para logging — d4_key/b son auth, no se loggean en claro.
        var maskedUrl = $"{CopsisBaseUrl}?d4_key={Mask(axa.CopsisD4Key)}&b={Mask(axa.CopsisB)}";

        // Body que COPSIS espera (replica del legacy CotizacionNegocio.cs:5384-5391):
        //   { s4_key: <d4_key>, b: <b>, v1: <SOLICITUDEMISION xml> }
        // El campo del body es "s4_key" (no "d4_key" — el legacy lo escribe así desde hace años,
        // posible typo del contrato COPSIS que ya está estable). v1 es el XML construido por
        // AxaDxnCopsisEmitRequestBuilder según el contrato SOLICITUDEMISION.
        var v1 = AxaDxnCopsisEmitRequestBuilder.BuildSolicitudEmisionXml(request, axa);
        var payload = new
        {
            s4_key = axa.CopsisD4Key,
            b = axa.CopsisB,
            v1 = v1,
        };

        var jsonBody = JsonSerializer.Serialize(payload);
        using var content = new StringContent(jsonBody, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };

        using var http = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        http.Headers.TryAddWithoutValidation("X-Correlation-Id", request.CorrelationId);

        var sw = Stopwatch.StartNew();
        _logger.LogInformation(
            "COPSIS request → {Url} numCotizacion={NumCotizacion} bodyBytes={BodyBytes}",
            maskedUrl, request.ExternalQuoteRef, jsonBody.Length);

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(CopsisTimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using var response = await _http.SendAsync(http, linked.Token).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
            sw.Stop();

            // Log explícito siempre — éxito o falla — para que el diagnóstico
            // desde api.log no requiera abrir el blob. Truncamos a 2000 chars
            // para no inflar logs si COPSIS devuelve algo desmedido.
            var bodyForLog = responseBody.Length > 2000 ? responseBody[..2000] + "…[truncated]" : responseBody;
            _logger.LogInformation(
                "COPSIS response ← status={Status} latency={LatencyMs}ms body={Body}",
                (int)response.StatusCode, sw.ElapsedMilliseconds, bodyForLog);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "COPSIS HTTP {Status} {Reason} — body: {Body}",
                    (int)response.StatusCode, response.ReasonPhrase, bodyForLog);
                return Fail($"HTTP_{(int)response.StatusCode}",
                    $"COPSIS respondió HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
                    ErrorCategory.InsurerDown,
                    (int)sw.ElapsedMilliseconds, jsonBody, responseBody);
            }

            var outcome = AxaDxnCopsisEmitResponseParser.Parse(jsonBody, responseBody, (int)sw.ElapsedMilliseconds);
            if (outcome is InsurerEmitOutcome.Failure parseFailure)
            {
                _logger.LogWarning(
                    "COPSIS payload error code={Code} msg={Msg}",
                    parseFailure.Error.ExternalCode, parseFailure.Error.ExternalMessage);
            }
            else if (outcome is InsurerEmitOutcome.Success ok)
            {
                _logger.LogInformation(
                    "COPSIS issued policy {PolicyNumber} pdfUrl={PdfUrl}",
                    ok.Response.PolicyNumber, ok.Response.PdfDownloadUrl ?? "(none)");
            }
            return outcome;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return Fail("COPSIS_DOWN",
                $"COPSIS no respondió en {CopsisTimeoutSeconds}s.",
                ErrorCategory.InsurerDown,
                (int)sw.ElapsedMilliseconds, jsonBody, rawResponse: null);
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            return Fail("HTTP_ERROR",
                $"Error de red contra COPSIS: {ex.Message}",
                ErrorCategory.InsurerDown,
                (int)sw.ElapsedMilliseconds, jsonBody, rawResponse: null);
        }
    }

    private static InsurerEmitOutcome Fail(
        string code, string message, ErrorCategory category,
        int latencyMs, string? rawRequest, string? rawResponse) =>
        new InsurerEmitOutcome.Failure(new InsurerEmitError(
            category, code, message, latencyMs, rawRequest, rawResponse));

    // Enmascara una credencial para logs: primeros 3 + "***" + últimos 2.
    // Strings <6 chars salen como "***" — no merece preservar 1 char.
    private static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "(empty)";
        if (value.Length < 6) return "***";
        return $"{value[..3]}***{value[^2..]}";
    }
}
