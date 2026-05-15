namespace McBrokers.Application.Ports;

public interface IBlobStore
{
    /// <summary>
    /// Persiste un blob de texto (XML/JSON/etc) bajo el path indicado y
    /// devuelve la referencia (URL local o Azure) para guardar en BD.
    /// path es la ruta completa relativa al root — los callers la componen
    /// usando McBrokers.Application.Blob.BlobPaths para mantener convención.
    /// </summary>
    Task<string> WriteAsync(
        string path,
        string content,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken);

    /// <summary>
    /// Persiste un blob binario (PDF, imagen, etc) bajo el path indicado.
    /// </summary>
    Task<string> WriteBinaryAsync(
        string path,
        byte[] content,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lee un blob de texto por la referencia devuelta en Write*.
    /// Local: file://... ; Azure: https://...
    /// </summary>
    Task<string?> ReadAsync(string reference, CancellationToken cancellationToken);

    /// <summary>
    /// Lee un blob binario por su ruta relativa al container (la misma que
    /// se pasó a WriteBinaryAsync). Devuelve null si la ruta es vacía o el
    /// blob no existe.
    ///
    /// Asimétrico con ReadAsync a propósito: los PDFs se sirven a navegadores
    /// (visor de póliza) y el caller solo conoce la ruta canónica, no la
    /// URL/reference de cada backend. Mantener path como identidad permite
    /// que el mismo PdfBlobRef funcione en LocalDisk y Azure sin parsing.
    /// </summary>
    Task<byte[]?> ReadBinaryAsync(string path, CancellationToken cancellationToken);
}
