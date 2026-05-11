namespace McBrokers.Application.Ports;

public interface IPdfDownloader
{
    /// <summary>
    /// Descarga el PDF desde la URL y devuelve los bytes. Lanza si la descarga falla.
    /// </summary>
    Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken);
}
