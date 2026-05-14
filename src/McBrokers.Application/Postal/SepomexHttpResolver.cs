using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using McBrokers.Application.Ports;
using McBrokers.SharedKernel;
using Microsoft.Extensions.Logging;

namespace McBrokers.Application.Postal;

// Cliente del microservicio SEPOMEX
// (https://consultarcp-api.azurewebsites.net/api/sepomex/{cp}).
// Devuelve estado + municipio + asentamientos para autocompletar el form
// de emisión cuando el vendedor ingresa el CP del asegurado. La URL base
// se configura via HttpClient.BaseAddress en DI desde "Sepomex:BaseUrl"
// en appsettings.
public sealed class SepomexHttpResolver : IPostalCodeResolver
{
    private static readonly System.Text.RegularExpressions.Regex CpPattern =
        new(@"^\d{5}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly ILogger<SepomexHttpResolver> _logger;

    public SepomexHttpResolver(HttpClient http, ILogger<SepomexHttpResolver> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<Result<PostalCodeInfo>> ResolveAsync(string codigoPostal, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(codigoPostal) || !CpPattern.IsMatch(codigoPostal))
        {
            return Result<PostalCodeInfo>.Failure("El código postal debe ser de 5 dígitos.");
        }

        try
        {
            using var response = await _http.GetAsync($"api/sepomex/{codigoPostal}", cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Result<PostalCodeInfo>.Failure($"Código postal {codigoPostal} no encontrado.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SEPOMEX returned HTTP {Status} for CP {Cp}", (int)response.StatusCode, codigoPostal);
                return Result<PostalCodeInfo>.Failure(
                    $"Servicio SEPOMEX no disponible (HTTP {(int)response.StatusCode}).");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return Parse(body, codigoPostal);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error calling SEPOMEX for CP {Cp}", codigoPostal);
            return Result<PostalCodeInfo>.Failure("Servicio SEPOMEX inalcanzable.");
        }
        catch (TaskCanceledException)
        {
            return Result<PostalCodeInfo>.Failure("Servicio SEPOMEX tardó demasiado en responder.");
        }
    }

    private static Result<PostalCodeInfo> Parse(string json, string cp)
    {
        try
        {
            var raw = JsonSerializer.Deserialize<RawResponse>(json, JsonOpts);
            if (raw is null || string.IsNullOrWhiteSpace(raw.Estado))
            {
                return Result<PostalCodeInfo>.Failure($"Respuesta SEPOMEX para {cp} sin Estado.");
            }

            var asentamientos = (raw.Asentamientos ?? new List<RawAsentamiento>())
                .Where(a => !string.IsNullOrWhiteSpace(a.Colonia))
                .Select(a => new Asentamiento(a.Colonia!, a.TipoAsentamiento ?? string.Empty))
                .ToList();

            return Result<PostalCodeInfo>.Success(new PostalCodeInfo(
                raw.Cp ?? cp,
                raw.Estado,
                raw.Municipio ?? string.Empty,
                asentamientos));
        }
        catch (JsonException ex)
        {
            return Result<PostalCodeInfo>.Failure($"Respuesta SEPOMEX no es JSON válido: {ex.Message}");
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed record RawResponse(
        [property: JsonPropertyName("CP")] string? Cp,
        [property: JsonPropertyName("Estado")] string? Estado,
        [property: JsonPropertyName("Municipio")] string? Municipio,
        [property: JsonPropertyName("Asentamientos")] List<RawAsentamiento>? Asentamientos);

    private sealed record RawAsentamiento(
        [property: JsonPropertyName("Colonia")] string? Colonia,
        [property: JsonPropertyName("Tipo_Asentamiento")] string? TipoAsentamiento);
}
