using McBrokers.Application.Ports;
using Microsoft.Extensions.Logging;

namespace McBrokers.Infrastructure.Pdf;

public sealed class HttpPdfDownloader : IPdfDownloader
{
    private readonly HttpClient _http;
    private readonly ILogger<HttpPdfDownloader> _logger;

    public HttpPdfDownloader(HttpClient http, ILogger<HttpPdfDownloader> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL must not be empty.", nameof(url));
        }

        _logger.LogDebug("Downloading PDF from {Url}", url);
        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }
}
