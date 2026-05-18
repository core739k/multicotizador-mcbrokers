using McBrokers.SharedKernel;

namespace McBrokers.Application.Ports;

public interface IAxaDxnCatalogClient
{
    Task<Result<IReadOnlyList<AxaDxnCatalogRecord>>> FetchAsync(
        AxaDxnCatalogCredentials credentials,
        string tarifa,
        string nombreCatalogo,
        CancellationToken cancellationToken);
}

public sealed record AxaDxnCatalogCredentials(string Usuario, string Password, string EndpointUrl);

public sealed record AxaDxnCatalogRecord(
    string? IdMarca,
    string? IdTipoVehiculo,
    string? Descripcion,
    string? IdTipo,
    string? ClaveAmis,
    int? ModeloDesde,
    int? ModeloHasta);
