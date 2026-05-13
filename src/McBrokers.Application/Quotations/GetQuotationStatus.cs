using System.Text.Json;
using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Quotations;

namespace McBrokers.Application.Quotations;

public sealed record QuotationStatusView(
    Guid Id,
    string CorrelationId,
    QuotationStatus Status,
    int ExpectedResultsCount,
    decimal SumInsured,
    QuotationVehicleView? Vehicle,
    QuotationDeduciblesView? Deducibles,
    IReadOnlyList<QuotationResultView> Results);

public sealed record QuotationVehicleView(int Year, string Brand, string Model, string Version);

// Snapshot parseado desde CustomerSnapshotJson. Transitorio hasta que F4 Fase B
// normalice los deducibles como campos persistidos de Quotation.
public sealed record QuotationDeduciblesView(
    decimal MaterialDamagesDeductiblePct,
    decimal RobberyDeductiblePct,
    decimal MedicalExpensesSumInsured,
    decimal CivilLiabilitySumInsured);

public sealed record QuotationResultView(
    Guid Id,
    Guid InsurerId,
    InsurerCode? InsurerCode,
    string? InsurerName,
    string? InsurerLogoUrl,
    QuotationInsurerStatus Status,
    ErrorCategory ErrorCategory,
    string? ErrorCode,
    string? ErrorMessage,
    decimal? PremiumTotal,
    decimal? PremiumNet,
    decimal? Tax,
    decimal? Fees,
    int LatencyMs,
    string? ExternalQuoteRef,
    IReadOnlyList<CoverageBadge> CoverageBadges);

public sealed class GetQuotationStatus
{
    private readonly IQuotationRepository _quotations;
    private readonly IVehicleMasterRepository _vehicles;
    private readonly IInsurerRepository _insurers;

    public GetQuotationStatus(
        IQuotationRepository quotations,
        IVehicleMasterRepository vehicles,
        IInsurerRepository insurers)
    {
        _quotations = quotations;
        _vehicles = vehicles;
        _insurers = insurers;
    }

    public async Task<QuotationStatusView?> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var q = await _quotations.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (q is null) return null;

        var master = await _vehicles.GetByIdAsync(q.VehicleMasterId, cancellationToken).ConfigureAwait(false);
        var vehicle = master is null
            ? null
            : new QuotationVehicleView(master.Year, master.Brand, master.Model, master.Version);

        var insurers = await _insurers.ListAsync(cancellationToken).ConfigureAwait(false);
        var byId = insurers.ToDictionary(i => i.Id);

        return new QuotationStatusView(
            q.Id, q.CorrelationId, q.Status, q.ExpectedResultsCount,
            SumInsured: q.SumInsured,
            Vehicle: vehicle,
            Deducibles: ParseDeducibles(q.CustomerSnapshotJson),
            Results: q.Results.Select(r => ProjectResult(r, byId, q.Package)).ToList());
    }

    private static QuotationResultView ProjectResult(
        QuotationInsurerResult r,
        IReadOnlyDictionary<Guid, Insurer> byId,
        PackageCode package)
    {
        var insurer = byId.GetValueOrDefault(r.InsurerId);
        // LogoUrl explícito de la BD si está, si no fallback al asset del wwwroot
        // vía InsurerLogoMapping. Para Insurer no encontrado (raro: cambio de id
        // o catálogo desincronizado) el view sigue siendo coherente con null/null.
        var logoUrl = insurer?.LogoUrl ?? (insurer is null
            ? null
            : InsurerLogoMapping.DefaultRelativeUrl(insurer.Code));

        // Coverage badges derivados del paquete y la aseguradora — el WS no devuelve
        // este detalle (solo totales de prima), así que es matriz determinista hoy.
        // Sin insurer (caso defensivo) → lista vacía.
        var coverageBadges = insurer is null
            ? Array.Empty<CoverageBadge>()
            : PackageCoverageMatrix.Compute(insurer.Code, package);

        return new QuotationResultView(
            r.Id, r.InsurerId,
            insurer?.Code,
            insurer?.Name,
            logoUrl,
            r.Status, r.ErrorCategory,
            r.ErrorCode, r.ErrorMessageHuman,
            r.PremiumTotal, r.PremiumNet, r.Tax, r.Fees,
            r.LatencyMs, r.ExternalQuoteRef,
            coverageBadges);
    }

    // Parser tolerante: snapshots viejos o malformados devuelven null sin crashear.
    // El payload esperado lo produce IndexModel.OnPostAsync:
    //   { "Deductibles": { "MaterialDamagesDeductiblePct": 5, ... } }
    private static QuotationDeduciblesView? ParseDeducibles(string snapshotJson)
    {
        if (string.IsNullOrWhiteSpace(snapshotJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(snapshotJson);
            if (!doc.RootElement.TryGetProperty("Deductibles", out var deds)) return null;

            return new QuotationDeduciblesView(
                MaterialDamagesDeductiblePct: ReadDecimal(deds, "MaterialDamagesDeductiblePct"),
                RobberyDeductiblePct: ReadDecimal(deds, "RobberyDeductiblePct"),
                MedicalExpensesSumInsured: ReadDecimal(deds, "MedicalExpensesSumInsured"),
                CivilLiabilitySumInsured: ReadDecimal(deds, "CivilLiabilitySumInsured"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static decimal ReadDecimal(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var v) && v.TryGetDecimal(out var d) ? d : 0m;
}
