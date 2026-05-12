using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using Microsoft.Extensions.Logging;

namespace McBrokers.Insurers.Gnp.Mapping;

/// <summary>
/// Ejecuta el flujo de emisión GNP: POST a /emisor/emitir + (si OK) POST a /impresion/buscarPoliza
/// para obtener URL del PDF. Reintentos de impresión 3 × 5s según la documentación.
/// </summary>
public static class GnpEmissionExecutor
{
    private const string EmissionEndpoint = "https://api.service.gnp.com.mx/autos/wsp/emisor/emisor/emitir";
    private const string PrintEndpoint = "https://api.service.gnp.com.mx/autos/wsp/impresion/buscarPoliza";

    public static async Task<InsurerEmitOutcome> ExecuteAsync(
        HttpClient http, ILogger logger, TimeProvider time,
        InsurerEmitRequest request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var requestXml = GnpEmissionBuilder.BuildEmissionXml(request, today);

        var sw = Stopwatch.StartNew();
        string emissionResponse;
        try
        {
            emissionResponse = await PostXmlAsync(http, EmissionEndpoint, requestXml, request, cancellationToken).ConfigureAwait(false);
            sw.Stop();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return new InsurerEmitOutcome.Failure(new InsurerEmitError(
                ErrorCategory.InsurerDown, "TIMEOUT",
                $"GNP emisión no respondió en {request.Connection.TimeoutSeconds}s.",
                (int)sw.ElapsedMilliseconds, requestXml, null));
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            return new InsurerEmitOutcome.Failure(new InsurerEmitError(
                ErrorCategory.InsurerDown, "HTTP_ERROR",
                $"Error de red en emisión GNP: {ex.Message}",
                (int)sw.ElapsedMilliseconds, requestXml, null));
        }

        var emissionOutcome = GnpEmissionParser.Parse(requestXml, emissionResponse, (int)sw.ElapsedMilliseconds);
        if (emissionOutcome is not InsurerEmitOutcome.Success success)
        {
            return emissionOutcome;
        }

        // Llamada a buscarPoliza con 3 reintentos de 5s entre cada uno (según la doc).
        var pdfUrl = await TryFetchPdfUrlAsync(
            http, logger, request, success.Response.PolicyNumber, cancellationToken).ConfigureAwait(false);

        return new InsurerEmitOutcome.Success(success.Response with { PdfDownloadUrl = pdfUrl });
    }

    private static async Task<string?> TryFetchPdfUrlAsync(
        HttpClient http, ILogger logger,
        InsurerEmitRequest request, string policyNumber, CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        var printXml = GnpEmissionBuilder.BuildPrintRequest(
            request.Credentials.Username, request.Credentials.Password, policyNumber);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var raw = await PostXmlAsync(http, PrintEndpoint, printXml, request, cancellationToken).ConfigureAwait(false);
                var url = GnpEmissionParser.ParsePdfUrlFromPrintResponse(raw);
                if (!string.IsNullOrWhiteSpace(url)) return url;

                logger.LogWarning(
                    "GNP buscarPoliza attempt {Attempt}/{Max} returned no URL for policy {Policy}",
                    attempt, maxAttempts, policyNumber);
            }
            catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException)
            {
                logger.LogWarning(ex,
                    "GNP buscarPoliza attempt {Attempt}/{Max} failed for policy {Policy}",
                    attempt, maxAttempts, policyNumber);
            }

            if (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
        }

        logger.LogWarning("GNP buscarPoliza exhausted retries for policy {Policy}", policyNumber);
        return null;
    }

    private static async Task<string> PostXmlAsync(
        HttpClient http, string endpoint, string body, InsurerEmitRequest request, CancellationToken cancellationToken)
    {
        using var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/xml");

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
        httpReq.Headers.TryAddWithoutValidation("X-Correlation-Id", request.CorrelationId);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(request.Connection.TimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        using var response = await http.SendAsync(httpReq, linked.Token).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"GNP responded HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
        }

        return raw;
    }
}
