namespace McBrokers.Application.Ports;

public interface IBlobStore
{
    /// <summary>
    /// Persiste un blob de texto (XML/JSON/etc) y devuelve la referencia para guardar en BD.
    /// </summary>
    Task<string> WriteAsync(
        string container,
        string blobName,
        string content,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken);
}
