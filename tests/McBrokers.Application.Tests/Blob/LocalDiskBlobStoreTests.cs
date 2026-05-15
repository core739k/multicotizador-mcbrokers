using McBrokers.Infrastructure.Blob;
using Microsoft.Extensions.Logging.Abstractions;

namespace McBrokers.Application.Tests.Blob;

// Roundtrip Write→Read del LocalDiskBlobStore — el contrato que la página
// /Admin/Quotations usa para descargar XMLs.
public class LocalDiskBlobStoreTests : IDisposable
{
    private readonly string _root;

    public LocalDiskBlobStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"mcbrokers-test-{Guid.NewGuid():n}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public async Task Write_then_Read_returns_same_content()
    {
        var store = new LocalDiskBlobStore(_root, NullLogger<LocalDiskBlobStore>.Instance);
        var content = "<?xml version=\"1.0\"?><sample>data</sample>";
        var reference = await store.WriteAsync(
            path: "2024/VW/JETTA/corr-abc/cotizacion-Gnp-request.xml",
            content: content,
            metadata: null,
            cancellationToken: CancellationToken.None);

        var readBack = await store.ReadAsync(reference, CancellationToken.None);

        readBack.Should().Be(content);
    }

    [Fact]
    public async Task Write_creates_nested_directories_for_path()
    {
        var store = new LocalDiskBlobStore(_root, NullLogger<LocalDiskBlobStore>.Instance);
        await store.WriteAsync(
            path: "2024/ACURA/MDX/abc/cotizacion-Gnp-request.xml",
            content: "data", metadata: null, cancellationToken: CancellationToken.None);

        File.Exists(Path.Combine(_root, "2024", "ACURA", "MDX", "abc", "cotizacion-Gnp-request.xml"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Read_returns_null_when_reference_is_blank_or_missing()
    {
        var store = new LocalDiskBlobStore(_root, NullLogger<LocalDiskBlobStore>.Instance);

        (await store.ReadAsync("", CancellationToken.None)).Should().BeNull();
        (await store.ReadAsync("file:///c:/does/not/exist.xml", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Read_handles_reference_without_file_scheme()
    {
        // Si alguien guarda la referencia desnuda en BD (sin file://), debería
        // funcionar igual — pragmatismo defensivo.
        var store = new LocalDiskBlobStore(_root, NullLogger<LocalDiskBlobStore>.Instance);
        var reference = await store.WriteAsync("2024/X/Y/c/file.txt", "hola", null, CancellationToken.None);

        var bare = reference.Replace("file://", string.Empty);
        var content = await store.ReadAsync(bare, CancellationToken.None);

        content.Should().Be("hola");
    }

    [Fact]
    public async Task ReadBinaryAsync_returns_bytes_written_under_relative_path()
    {
        // Visor de póliza: PdfBlobRef guarda la ruta relativa del container
        // (ej. "2024/ACURA/MDX/{cid}/poliza-AxaDxn.pdf") y el endpoint del Api
        // lee directo por ese path — sin parsear file:// ni URLs.
        var store = new LocalDiskBlobStore(_root, NullLogger<LocalDiskBlobStore>.Instance);
        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }; // "%PDF-"
        const string path = "2024/ACURA/MDX/cid-abc/poliza-AxaDxn.pdf";
        await store.WriteBinaryAsync(path, bytes, metadata: null, CancellationToken.None);

        var read = await store.ReadBinaryAsync(path, CancellationToken.None);

        read.Should().Equal(bytes);
    }

    [Fact]
    public async Task ReadBinaryAsync_returns_null_for_missing_or_blank_path()
    {
        var store = new LocalDiskBlobStore(_root, NullLogger<LocalDiskBlobStore>.Instance);

        (await store.ReadBinaryAsync("", CancellationToken.None)).Should().BeNull();
        (await store.ReadBinaryAsync("2024/MISSING/file.pdf", CancellationToken.None)).Should().BeNull();
    }
}
