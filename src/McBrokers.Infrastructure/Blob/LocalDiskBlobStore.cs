using McBrokers.Application.Ports;
using Microsoft.Extensions.Logging;

namespace McBrokers.Infrastructure.Blob;

/// <summary>
/// Implementación de desarrollo: escribe a disco local bajo {rootPath}/{container}/{blobName}.
/// En producción usar AzureBlobStore (pendiente, F0.H provisioning).
/// </summary>
public sealed class LocalDiskBlobStore : IBlobStore
{
    private readonly string _rootPath;
    private readonly ILogger<LocalDiskBlobStore> _logger;

    public LocalDiskBlobStore(string rootPath, ILogger<LocalDiskBlobStore> logger)
    {
        _rootPath = rootPath;
        _logger = logger;
    }

    public async Task<string> WriteAsync(
        string container, string blobName, string content,
        IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(_rootPath, container, blobName);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content, cancellationToken).ConfigureAwait(false);

        if (metadata is { Count: > 0 })
        {
            var metaPath = fullPath + ".meta";
            await File.WriteAllLinesAsync(
                metaPath,
                metadata.Select(kv => $"{kv.Key}={kv.Value}"),
                cancellationToken).ConfigureAwait(false);
        }

        var reference = $"file://{fullPath.Replace('\\', '/')}";
        _logger.LogDebug("Wrote local blob {Reference} ({Size} chars)", reference, content.Length);
        return reference;
    }

    public async Task<string> WriteBinaryAsync(
        string container, string blobName, byte[] content,
        IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(_rootPath, container, blobName);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(fullPath, content, cancellationToken).ConfigureAwait(false);

        if (metadata is { Count: > 0 })
        {
            var metaPath = fullPath + ".meta";
            await File.WriteAllLinesAsync(
                metaPath,
                metadata.Select(kv => $"{kv.Key}={kv.Value}"),
                cancellationToken).ConfigureAwait(false);
        }

        var reference = $"file://{fullPath.Replace('\\', '/')}";
        _logger.LogDebug("Wrote local binary blob {Reference} ({Size} bytes)", reference, content.Length);
        return reference;
    }
}
