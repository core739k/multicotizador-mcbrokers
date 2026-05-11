using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using McBrokers.Insurers.Abstractions;
using McBrokers.Insurers.Abstractions.Soap;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace McBrokers.Insurers.Ana;

public sealed record AnaPostalInfo(string EdoMun, string Poblacion);

public interface IAnaPostalCodeResolver
{
    Task<AnaPostalInfo> ResolveAsync(string postalCode, InsurerCredentials credentials, string endpoint, CancellationToken cancellationToken);
}

/// <summary>
/// Llama a ColoniaxCPAsync de WSCOR para resolver el EdoMun (5 dígitos = estado 2 + municipio 3)
/// + nombre del municipio a partir del CP. Cachea por CP durante 24h.
/// </summary>
public sealed class AnaPostalCodeResolver : IAnaPostalCodeResolver
{
    private static readonly XNamespace AnaNs = "http://server.anaseguros.com.mx/WSCOR/";

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AnaPostalCodeResolver> _logger;

    public AnaPostalCodeResolver(HttpClient http, IMemoryCache cache, ILogger<AnaPostalCodeResolver> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<AnaPostalInfo> ResolveAsync(
        string postalCode, InsurerCredentials credentials, string endpoint, CancellationToken cancellationToken)
    {
        var cacheKey = $"ana:cp:{postalCode}";
        if (_cache.TryGetValue<AnaPostalInfo>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var fallback = BuildFallback(postalCode);

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return fallback;
        }

        try
        {
            var body = new XElement(AnaNs + "ColoniaxCP",
                new XElement(AnaNs + "Negocio", credentials.BusinessUnit ?? string.Empty),
                new XElement(AnaNs + "CPostal", postalCode),
                new XElement(AnaNs + "Usuario", credentials.Username),
                new XElement(AnaNs + "Clave", credentials.Password));
            var soap = SoapEnvelope.Wrap(SoapVersion.Soap11, body);

            using var content = new StringContent(soap, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };

            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
            req.Headers.TryAddWithoutValidation("SOAPAction",
                "\"http://server.anaseguros.com.mx/WSCOR/ColoniaxCP\"");

            using var response = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ANA ColoniaxCP returned HTTP {Status} for CP {Cp}",
                    (int)response.StatusCode, postalCode);
                return fallback;
            }

            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var info = ParseResponse(raw) ?? fallback;

            _cache.Set(cacheKey, info, TimeSpan.FromHours(24));
            return info;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ANA ColoniaxCP failed for CP {Cp}; falling back to placeholder", postalCode);
            return fallback;
        }
    }

    public static AnaPostalInfo? ParseResponse(string rawResponse)
    {
        XDocument doc;
        try { doc = XDocument.Parse(rawResponse); }
        catch { return null; }

        // El response embebe XML serializado dentro de ColoniaxCPResult.
        var resultEl = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ColoniaxCPResult");
        if (resultEl is null) return null;

        // El contenido puede ser XML estructurado in-place o un string XML embebido.
        var firstChild = resultEl.Elements().FirstOrDefault();
        XDocument inner;
        if (firstChild is not null)
        {
            inner = new XDocument(new XElement("root", resultEl.Elements()));
        }
        else
        {
            try { inner = XDocument.Parse(resultEl.Value); }
            catch { return null; }
        }

        var colonia = inner.Descendants().FirstOrDefault(e => e.Name.LocalName == "colonia");
        if (colonia is null) return null;

        var idEstado = (LocalChild(colonia, "IdEstado") ?? string.Empty).PadLeft(2, '0');
        var idDelMun = (LocalChild(colonia, "IdDelMun") ?? string.Empty).PadLeft(3, '0');
        var delMun = LocalChild(colonia, "DelMun") ?? string.Empty;

        if (idEstado.Length != 2 || idDelMun.Length != 3) return null;

        return new AnaPostalInfo(EdoMun: idEstado + idDelMun, Poblacion: delMun);
    }

    private static string? LocalChild(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;

    private static AnaPostalInfo BuildFallback(string postalCode)
    {
        var safe = (postalCode ?? string.Empty).PadLeft(5, '0');
        return new AnaPostalInfo(EdoMun: safe, Poblacion: string.Empty);
    }
}
