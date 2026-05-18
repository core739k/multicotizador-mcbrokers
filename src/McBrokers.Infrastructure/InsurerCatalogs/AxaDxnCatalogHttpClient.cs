using System.Net.Http.Headers;
using System.Text;
using McBrokers.Application.Ports;
using McBrokers.Insurers.AxaDxn.Mapping.Catalog;
using McBrokers.SharedKernel;
using Microsoft.Extensions.Logging;

namespace McBrokers.Infrastructure.InsurerCatalogs;

/// <summary>
/// Cliente HTTP/SOAP del WS de catálogo AXA DXN (getCatalogosPorTarifaYNombre).
/// Compone el envelope con AxaDxnCatalogSoapBuilder, hace POST con Basic Auth
/// usando las credenciales recibidas, y delega el parsing a
/// AxaDxnCatalogResponseParser. Mapea AxaDxnCatalogRawRow (Insurers.AxaDxn) a
/// AxaDxnCatalogRecord (Application) para respetar la dirección de dependencias.
/// </summary>
public sealed class AxaDxnCatalogHttpClient : IAxaDxnCatalogClient
{
    private readonly HttpClient _http;
    private readonly ILogger<AxaDxnCatalogHttpClient> _logger;

    public AxaDxnCatalogHttpClient(HttpClient http, ILogger<AxaDxnCatalogHttpClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<AxaDxnCatalogRecord>>> FetchAsync(
        AxaDxnCatalogCredentials credentials,
        string tarifa,
        string nombreCatalogo,
        CancellationToken cancellationToken)
    {
        var soapXml = AxaDxnCatalogSoapBuilder.Build(tarifa, nombreCatalogo);

        _logger.LogInformation(
            "AXA DXN CATALOG REQUEST tarifa={Tarifa} catalogo={Catalogo} endpoint={Endpoint} body={Body}",
            tarifa, nombreCatalogo, credentials.EndpointUrl, soapXml);

        using var content = new StringContent(soapXml, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };

        using var request = new HttpRequestMessage(HttpMethod.Post, credentials.EndpointUrl)
        {
            Content = content,
        };

        var basicAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes(
            $"{credentials.Usuario}:{credentials.Password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
        request.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // Loguear el body (truncado a 2KB) ayuda a diagnosticar fallos del WS de AXA
                // donde el SOAP Fault de Axis trae el stack trace Java en el cuerpo.
                var truncated = body.Length > 2048 ? body[..2048] + "…(truncated)" : body;
                _logger.LogWarning(
                    "AXA DXN catálogo respondió HTTP {Status} para tarifa={Tarifa} catalogo={Catalogo}. Body: {Body}",
                    (int)response.StatusCode, tarifa, nombreCatalogo, truncated);
                return Result<IReadOnlyList<AxaDxnCatalogRecord>>.Failure(
                    $"HTTP_{(int)response.StatusCode}: {response.ReasonPhrase}");
            }

            var parsed = AxaDxnCatalogResponseParser.Parse(body);
            if (!parsed.IsSuccess)
            {
                return Result<IReadOnlyList<AxaDxnCatalogRecord>>.Failure(parsed.Error);
            }

            var mapped = parsed.Value
                .Select(r => new AxaDxnCatalogRecord(
                    r.IdMarca, r.IdTipoVehiculo, r.Descripcion, r.IdTipo,
                    r.ClaveAmis, r.ModeloDesde, r.ModeloHasta))
                .ToArray();

            return Result<IReadOnlyList<AxaDxnCatalogRecord>>.Success(mapped);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<IReadOnlyList<AxaDxnCatalogRecord>>.Failure(
                $"TIMEOUT: tarifa={tarifa} catalogo={nombreCatalogo}");
        }
        catch (HttpRequestException ex)
        {
            return Result<IReadOnlyList<AxaDxnCatalogRecord>>.Failure(
                $"HTTP_ERROR: {ex.Message}");
        }
    }
}
