using McBrokers.SharedKernel;

namespace McBrokers.Application.Catalog;

public interface IImportInsurerCatalog
{
    Task<Result<ImportInsurerCatalogResult>> ExecuteAsync(
        ImportInsurerCatalogCommand command, CancellationToken cancellationToken);
}
