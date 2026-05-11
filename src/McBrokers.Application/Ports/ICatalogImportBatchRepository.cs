using McBrokers.Domain.Catalog;

namespace McBrokers.Application.Ports;

public interface ICatalogImportBatchRepository
{
    Task<IReadOnlyList<CatalogImportBatch>> ListRecentAsync(int take, CancellationToken cancellationToken);

    Task AddAsync(CatalogImportBatch batch, CancellationToken cancellationToken);

    Task UpdateAsync(CatalogImportBatch batch, CancellationToken cancellationToken);
}
