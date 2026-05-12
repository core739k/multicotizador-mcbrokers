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
    // TODO Fase 5+ — mover a configuración / Key Vault. Hoy son constantes scaffold
    // hasta que MCBrokers provea los valores productivos de COPSIS.
    private const string CopsisBaseUrl = "https://lb1.copsis.com/sio4apolizas-lazy-fetch/EmisionIncisoAxaAPI";
    private const string D4Key = "REPLACE_ME_D4_KEY";
    private const string BParam = "REPLACE_ME_B";
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

        var url = $"{CopsisBaseUrl}?d4_key={Uri.EscapeDataString(D4Key)}&b={Uri.EscapeDataString(BParam)}";

        // Payload JSON — estructura aproximada según Documentación/Servicio AXA — Detalle Técnico.
        // El campo numCotizacion es el folio devuelto por la cotización (ExternalQuoteRef).
        var payload = new
        {
            numCotizacion = request.ExternalQuoteRef,
            polizaAutos = axa.PolizaAutos,
            tarifa = axa.Tarifa,
            tarifaPickup = axa.TarifaPickup,
            mesPoliza = axa.BusinessMes,
            vehiculo = new
            {
                marca = request.Vehicle.Brand,
                modelo = request.Vehicle.Year,
                version = request.Vehicle.Version,
                serie = request.Vehicle.SerialNumber,
                motor = request.Vehicle.EngineNumber,
                placa = request.Vehicle.Plate,
            },
            contratante = new
            {
                nombre = request.Contractor.FirstName,
                apellidoPaterno = request.Contractor.LastNamePaternal,
                apellidoMaterno = request.Contractor.LastNameMaternal,
                rfc = request.Contractor.Rfc,
                calle = request.Contractor.Street,
                numExterior = request.Contractor.ExteriorNumber,
                numInterior = request.Contractor.InteriorNumber,
                colonia = request.Contractor.Neighborhood,
                ciudad = request.Contractor.City,
                estado = request.Contractor.StateCode,
                cp = request.Contractor.PostalCode,
                telefono = request.Contractor.Phone,
                email = request.Contractor.Email,
            },
            primaNeta = request.PremiumNet,
            primaTotal = request.PremiumTotal,
            iva = request.Tax,
            derechos = request.Fees,
        };

        var jsonBody = JsonSerializer.Serialize(payload);
        using var content = new StringContent(jsonBody, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };

        using var http = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        http.Headers.TryAddWithoutValidation("X-Correlation-Id", request.CorrelationId);

        var sw = Stopwatch.StartNew();
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(CopsisTimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using var response = await _http.SendAsync(http, linked.Token).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("COPSIS returned HTTP {Status}", (int)response.StatusCode);
                return Fail($"HTTP_{(int)response.StatusCode}",
                    $"COPSIS respondió HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
                    ErrorCategory.InsurerDown,
                    (int)sw.ElapsedMilliseconds, jsonBody, responseBody);
            }

            return ParseCopsisResponse(jsonBody, responseBody, (int)sw.ElapsedMilliseconds);
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

    private static InsurerEmitOutcome ParseCopsisResponse(
        string rawRequest, string rawResponse, int latencyMs)
    {
        // Esperamos JSON {"polizaEmitida":"AXA12345","pdfUrl":"https://..."} u objeto con error.
        try
        {
            using var doc = JsonDocument.Parse(rawResponse);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
            {
                var code = root.TryGetProperty("codigo", out var c) ? c.GetString() ?? "UNKNOWN" : "UNKNOWN";
                var msg = error.GetString() ?? "COPSIS reportó error sin mensaje.";
                return Fail(code, msg, ErrorCategory.Business, latencyMs, rawRequest, rawResponse);
            }

            var polizaNumber = root.TryGetProperty("polizaEmitida", out var p) ? p.GetString() : null;
            var pdfUrl = root.TryGetProperty("pdfUrl", out var u) ? u.GetString() : null;

            if (string.IsNullOrWhiteSpace(polizaNumber))
            {
                return Fail("MISSING_POLIZA",
                    "COPSIS no devolvió polizaEmitida.",
                    ErrorCategory.Technical, latencyMs, rawRequest, rawResponse);
            }

            return new InsurerEmitOutcome.Success(new InsurerEmitResponse(
                PolicyNumber: polizaNumber!,
                PdfDownloadUrl: pdfUrl,
                LatencyMs: latencyMs,
                RawRequest: rawRequest,
                RawResponse: rawResponse));
        }
        catch (JsonException ex)
        {
            return Fail("PARSE_ERROR",
                $"Respuesta COPSIS no es JSON válido: {ex.Message}",
                ErrorCategory.Technical, latencyMs, rawRequest, rawResponse);
        }
    }

    private static InsurerEmitOutcome Fail(
        string code, string message, ErrorCategory category,
        int latencyMs, string? rawRequest, string? rawResponse) =>
        new InsurerEmitOutcome.Failure(new InsurerEmitError(
            category, code, message, latencyMs, rawRequest, rawResponse));
}
