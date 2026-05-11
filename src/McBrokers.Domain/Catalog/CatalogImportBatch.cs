using McBrokers.SharedKernel;

namespace McBrokers.Domain.Catalog;

public sealed class CatalogImportBatch
{
    public Guid Id { get; }
    public Guid InsurerId { get; }
    public string Source { get; }
    public DateTime StartedAt { get; }
    public DateTime? CompletedAt { get; private set; }
    public int RowsTotal { get; private set; }
    public int RowsAutoApproved { get; private set; }
    public int RowsPendingReview { get; private set; }
    public int RowsRejected { get; private set; }
    public Guid ImportedByAgentId { get; }

    private CatalogImportBatch(
        Guid id, Guid insurerId, string source, DateTime startedAt, Guid importedByAgentId)
    {
        Id = id;
        InsurerId = insurerId;
        Source = source;
        StartedAt = startedAt;
        ImportedByAgentId = importedByAgentId;
    }

    public static Result<CatalogImportBatch> Start(
        Guid insurerId, string source, DateTime startedAt, Guid importedByAgentId)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return Result<CatalogImportBatch>.Failure("Import source must not be empty.");
        }

        if (startedAt.Kind != DateTimeKind.Utc)
        {
            return Result<CatalogImportBatch>.Failure("startedAt must be UTC.");
        }

        return Result<CatalogImportBatch>.Success(new CatalogImportBatch(
            Guid.NewGuid(), insurerId, source.Trim(), startedAt, importedByAgentId));
    }

    public void Complete(int rowsTotal, int rowsAutoApproved, int rowsPendingReview, int rowsRejected, DateTime completedAt)
    {
        if (completedAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("completedAt must be UTC.", nameof(completedAt));
        }

        RowsTotal = rowsTotal;
        RowsAutoApproved = rowsAutoApproved;
        RowsPendingReview = rowsPendingReview;
        RowsRejected = rowsRejected;
        CompletedAt = completedAt;
    }
}
