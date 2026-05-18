namespace McBrokers.Application.Catalog.Importers;

public sealed record RunAxaDxnCatalogImportResult(
    Guid BatchId,
    int Total,
    int AutoApproved,
    int PendingReview,
    int Rejected);
