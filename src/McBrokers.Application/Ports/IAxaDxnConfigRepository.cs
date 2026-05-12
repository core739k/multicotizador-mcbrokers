using McBrokers.Domain.Insurers.AxaDxn;

namespace McBrokers.Application.Ports;

public interface IAxaDxnConfigRepository
{
    /// <summary>Carga el config del insurer junto con sus negocios (BIMBO/STRM/MCB/CAJA/CTBR).</summary>
    Task<AxaDxnConfigWithBusinesses?> GetByInsurerIdAsync(Guid insurerId, CancellationToken cancellationToken);

    Task AddAsync(AxaDxnConfig config, CancellationToken cancellationToken);
    Task UpdateAsync(AxaDxnConfig config, CancellationToken cancellationToken);

    Task<AxaDxnBusiness?> GetBusinessAsync(
        Guid axaDxnConfigId, AxaDxnBusinessName nombre, CancellationToken cancellationToken);

    Task AddBusinessAsync(AxaDxnBusiness business, CancellationToken cancellationToken);
    Task UpdateBusinessAsync(AxaDxnBusiness business, CancellationToken cancellationToken);
}

public sealed record AxaDxnConfigWithBusinesses(
    AxaDxnConfig Config,
    IReadOnlyList<AxaDxnBusiness> Businesses);
