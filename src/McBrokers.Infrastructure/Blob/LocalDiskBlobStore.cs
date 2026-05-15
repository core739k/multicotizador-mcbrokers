using McBrokers.Application.Ports;
using Microsoft.Extensions.Logging;

namespace McBrokers.Infrastructure.Blob;

/// <summary>
/// Implementación de desarrollo: escribe a disco local bajo {rootPath}/{path}.
/// Path es relativo y compuesto por callers via BlobPaths.* (estructura por
/// año/marca/modelo/correlationId). En producción usar AzureBlobStore.
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
        string path, string content,
        IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken)
    {
        var fullPath = ResolveFullPath(path);
        EnsureDirectory(fullPath);

        await File.WriteAllTextAsync(fullPath, content, cancellationToken).ConfigureAwait(false);
        await WriteMetadataAsync(fullPath, metadata, cancellationToken).ConfigureAwait(false);

        var reference = $"file://{fullPath.Replace('\\', '/')}";
        _logger.LogDebug("Wrote local blob {Reference} ({Size} chars)", reference, content.Length);
        return reference;
    }

    public async Task<string> WriteBinaryAsync(
        string path, byte[] content,
        IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken)
    {
        var fullPath = ResolveFullPath(path);
        EnsureDirectory(fullPath);

        await File.WriteAllBytesAsync(fullPath, content, cancellationToken).ConfigureAwait(false);
        await WriteMetadataAsync(fullPath, metadata, cancellationToken).ConfigureAwait(false);

        var reference = $"file://{fullPath.Replace('\\', '/')}";
        _logger.LogDebug("Wrote local binary blob {Reference} ({Size} bytes)", reference, content.Length);
        return reference;
    }

    public async Task<string?> ReadAsync(string reference, CancellationToken cancellationToken)
    {
        // Reference es "file://<rutaWindows>" devuelta por Write*.
        if (string.IsNullOrWhiteSpace(reference)) return null;
        const string filePrefix = "file://";
        var path = reference.StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase)
            ? reference[filePrefix.Length..].Replace('/', Path.DirectorySeparatorChar)
            : reference;
        if (!File.Exists(path)) return null;
        return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]?> ReadBinaryAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var fullPath = ResolveFullPath(path);
        if (!File.Exists(fullPath)) return null;
        return await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
    }

    private string ResolveFullPath(string path) =>
        Path.Combine(_rootPath, path.Replace('/', Path.DirectorySeparatorChar));

    private static void EnsureDirectory(string fullPath)
    {
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static async Task WriteMetadataAsync(
        string fullPath, IReadOnlyDictionary<string, string>? metadata, CancellationToken ct)
    {
        if (metadata is null || metadata.Count == 0) return;
        await File.WriteAllLinesAsync(
            fullPath + ".meta",
            metadata.Select(kv => $"{kv.Key}={kv.Value}"),
            ct).ConfigureAwait(false);
    }
}
