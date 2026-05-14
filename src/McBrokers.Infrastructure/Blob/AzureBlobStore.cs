using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using McBrokers.Application.Ports;
using Microsoft.Extensions.Logging;

namespace McBrokers.Infrastructure.Blob;

/// <summary>
/// Implementación Azure Blob Storage. Containers se asumen pre-creados
/// (ver INFRA_AZURE.md §4). Si quieres auto-crear en dev, agrega
/// CreateContainerOnDemand=true en config.
/// </summary>
public sealed class AzureBlobStore : IBlobStore
{
    private readonly BlobServiceClient _serviceClient;
    private readonly ILogger<AzureBlobStore> _logger;
    private readonly bool _createOnDemand;

    public AzureBlobStore(BlobServiceClient serviceClient, ILogger<AzureBlobStore> logger, bool createOnDemand)
    {
        _serviceClient = serviceClient;
        _logger = logger;
        _createOnDemand = createOnDemand;
    }

    public async Task<string> WriteAsync(
        string container, string blobName, string content,
        IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return await WriteCoreAsync(container, blobName, bytes, "application/xml", metadata, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> WriteBinaryAsync(
        string container, string blobName, byte[] content,
        IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken)
    {
        var contentType = blobName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? "application/pdf"
            : "application/octet-stream";
        return await WriteCoreAsync(container, blobName, content, contentType, metadata, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> ReadAsync(string reference, CancellationToken cancellationToken)
    {
        // Reference es la URL del blob devuelta en Write* (BlobClient.Uri).
        // Parseamos {Account}.blob.core.windows.net/{container}/{blob}.
        if (string.IsNullOrWhiteSpace(reference)) return null;
        if (!Uri.TryCreate(reference, UriKind.Absolute, out var uri)) return null;

        var segments = uri.AbsolutePath.TrimStart('/').Split('/', 2);
        if (segments.Length != 2) return null;

        var containerClient = _serviceClient.GetBlobContainerClient(segments[0]);
        var blobClient = containerClient.GetBlobClient(segments[1]);
        if (!await blobClient.ExistsAsync(cancellationToken).ConfigureAwait(false)) return null;

        var response = await blobClient.DownloadContentAsync(cancellationToken).ConfigureAwait(false);
        return response.Value.Content.ToString();
    }

    private async Task<string> WriteCoreAsync(
        string container, string blobName, byte[] content, string contentType,
        IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken)
    {
        var containerClient = _serviceClient.GetBlobContainerClient(container);
        if (_createOnDemand)
        {
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var blobClient = containerClient.GetBlobClient(blobName);
        using var ms = new MemoryStream(content);

        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
        };
        if (metadata is { Count: > 0 })
        {
            options.Metadata = metadata.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        await blobClient.UploadAsync(ms, options, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Wrote azure blob {Url} ({Size} bytes)", blobClient.Uri, content.Length);
        return blobClient.Uri.ToString();
    }
}
