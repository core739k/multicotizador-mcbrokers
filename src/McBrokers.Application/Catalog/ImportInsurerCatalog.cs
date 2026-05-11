using McBrokers.Application.Ports;
using McBrokers.Domain.Catalog;
using McBrokers.Domain.Catalog.Matching;
using McBrokers.SharedKernel;

namespace McBrokers.Application.Catalog;

public sealed record ImportInsurerCatalogCommand(
    Guid InsurerId,
    string Source,
    bool IsSourceOfTruth,
    IReadOnlyList<CatalogImportRow> Rows);

public sealed record ImportInsurerCatalogResult(
    Guid BatchId,
    int Total,
    int AutoApproved,
    int PendingReview,
    int Rejected);

public sealed class ImportInsurerCatalog
{
    private readonly IVehicleMasterRepository _masters;
    private readonly IVehicleInsurerMappingRepository _mappings;
    private readonly ICatalogImportBatchRepository _batches;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;
    private readonly ICurrentAgentProvider _currentAgent;
    private readonly TextNormalizer _normalizer;

    public ImportInsurerCatalog(
        IVehicleMasterRepository masters,
        IVehicleInsurerMappingRepository mappings,
        ICatalogImportBatchRepository batches,
        IAuditWriter audit,
        IClock clock,
        ICurrentAgentProvider currentAgent,
        TextNormalizer normalizer)
    {
        _masters = masters;
        _mappings = mappings;
        _batches = batches;
        _audit = audit;
        _clock = clock;
        _currentAgent = currentAgent;
        _normalizer = normalizer;
    }

    public async Task<Result<ImportInsurerCatalogResult>> ExecuteAsync(
        ImportInsurerCatalogCommand command, CancellationToken cancellationToken)
    {
        var batchResult = CatalogImportBatch.Start(
            command.InsurerId, command.Source, _clock.UtcNow, _currentAgent.AgentId);
        if (!batchResult.IsSuccess)
        {
            return Result<ImportInsurerCatalogResult>.Failure(batchResult.Error);
        }

        var batch = batchResult.Value;
        await _batches.AddAsync(batch, cancellationToken).ConfigureAwait(false);

        int approved = 0, pending = 0, rejected = 0;

        foreach (var row in command.Rows)
        {
            var outcome = await ImportRowAsync(command, row, cancellationToken).ConfigureAwait(false);
            switch (outcome)
            {
                case RowOutcome.Approved: approved++; break;
                case RowOutcome.Pending: pending++; break;
                case RowOutcome.Rejected: rejected++; break;
                case RowOutcome.Skipped: break;
            }
        }

        batch.Complete(command.Rows.Count, approved, pending, rejected, _clock.UtcNow);
        await _batches.UpdateAsync(batch, cancellationToken).ConfigureAwait(false);

        await _audit.WriteAsync(
            action: "Catalog.Import",
            entityType: "CatalogImportBatch",
            entityId: batch.Id.ToString(),
            payload: new { batch.InsurerId, batch.Source, command.IsSourceOfTruth, approved, pending, rejected },
            cancellationToken).ConfigureAwait(false);

        return Result<ImportInsurerCatalogResult>.Success(new ImportInsurerCatalogResult(
            batch.Id, command.Rows.Count, approved, pending, rejected));
    }

    private async Task<RowOutcome> ImportRowAsync(
        ImportInsurerCatalogCommand command, CatalogImportRow row, CancellationToken ct)
    {
        // Idempotency: same (insurer, externalClave) already imported → skip.
        var existing = await _mappings
            .FindByInsurerAndExternalClaveAsync(command.InsurerId, row.ExternalClave, ct)
            .ConfigureAwait(false);
        if (existing is not null) return RowOutcome.Skipped;

        var normBrand = _normalizer.Normalize(row.Brand);

        if (command.IsSourceOfTruth)
        {
            return await ImportSourceRowAsync(command, row, normBrand, ct).ConfigureAwait(false);
        }

        var candidates = await _masters
            .FindByYearAndBrandAsync(row.Year, normBrand, ct)
            .ConfigureAwait(false);

        if (candidates.Count == 0)
        {
            return RowOutcome.Rejected;
        }

        var rowDescriptor = NormalizeDescriptor(row.Model, row.Version);
        var scored = candidates
            .Select(c => new
            {
                Master = c,
                Score = TokenSetRatio.Score(rowDescriptor, NormalizeDescriptor(c.Model, c.Version)),
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        var topScore = scored[0].Score;
        var tied = scored.Count(s => s.Score == topScore) > 1;
        var winner = scored[0];

        // Force-route ties to manual review even with high score by capping the score
        // just under the auto-approval threshold.
        var effectiveScore = tied
            ? Math.Min(winner.Score, VehicleInsurerMapping.AutoApprovalThreshold - 0.01m)
            : winner.Score;

        var mappingResult = VehicleInsurerMapping.Create(
            winner.Master.Id,
            command.InsurerId,
            row.ExternalClave,
            row.Brand,
            row.Model,
            row.Version,
            effectiveScore,
            _clock.UtcNow);

        if (!mappingResult.IsSuccess) return RowOutcome.Rejected;

        await _mappings.AddAsync(mappingResult.Value, ct).ConfigureAwait(false);
        return mappingResult.Value.ReviewState == ReviewState.Approved ? RowOutcome.Approved : RowOutcome.Pending;
    }

    private async Task<RowOutcome> ImportSourceRowAsync(
        ImportInsurerCatalogCommand command, CatalogImportRow row, string normBrand, CancellationToken ct)
    {
        var masterResult = VehicleMaster.Create(
            row.Year, row.Brand, row.Model, row.Version,
            row.BodyType ?? string.Empty,
            ParseTransmission(row.Transmission),
            doors: 0, cylinders: 0);

        if (!masterResult.IsSuccess) return RowOutcome.Rejected;

        var master = masterResult.Value;
        await _masters.AddAsync(master, ct).ConfigureAwait(false);

        var mappingResult = VehicleInsurerMapping.Create(
            master.Id, command.InsurerId, row.ExternalClave,
            row.Brand, row.Model, row.Version,
            100m, _clock.UtcNow);

        if (!mappingResult.IsSuccess) return RowOutcome.Rejected;

        await _mappings.AddAsync(mappingResult.Value, ct).ConfigureAwait(false);
        return RowOutcome.Approved;
    }

    private string NormalizeDescriptor(string model, string version) =>
        _normalizer.Normalize($"{model} {version}");

    private static VehicleTransmission ParseTransmission(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return VehicleTransmission.Unknown;
        var s = raw.Trim().ToUpperInvariant();
        return s switch
        {
            "MANUAL" or "STD" or "ESTANDAR" => VehicleTransmission.Manual,
            "AUTOMATIC" or "AUT" or "AUTOMATICO" => VehicleTransmission.Automatic,
            "CVT" => VehicleTransmission.Cvt,
            _ => VehicleTransmission.Unknown,
        };
    }

    private enum RowOutcome { Approved, Pending, Rejected, Skipped }
}
