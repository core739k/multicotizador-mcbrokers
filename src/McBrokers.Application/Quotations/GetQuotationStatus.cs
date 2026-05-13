using System.Text.Json;
using McBrokers.Application.Ports;
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
    QuotationInsurerStatus Status,
    ErrorCategory ErrorCategory,
    string? ErrorCode,
    string? ErrorMessage,
    decimal? PremiumTotal,
    decimal? PremiumNet,
    decimal? Tax,
    decimal? Fees,
    int LatencyMs,
    string? ExternalQuoteRef);

public sealed class GetQuotationStatus
{
    private readonly IQuotationRepository _quotations;
    private readonly IVehicleMasterRepository _vehicles;

    public GetQuotationStatus(IQuotationRepository quotations, IVehicleMasterRepository vehicles)
    {
        _quotations = quotations;
        _vehicles = vehicles;
    }

    public async Task<QuotationStatusView?> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var q = await _quotations.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (q is null) return null;

        var master = await _vehicles.GetByIdAsync(q.VehicleMasterId, cancellationToken).ConfigureAwait(false);
        var vehicle = master is null
            ? null
            : new QuotationVehicleView(master.Year, master.Brand, master.Model, master.Version);

        return new QuotationStatusView(
            q.Id, q.CorrelationId, q.Status, q.ExpectedResultsCount,
            SumInsured: q.SumInsured,
            Vehicle: vehicle,
            Deducibles: ParseDeducibles(q.CustomerSnapshotJson),
            Results: q.Results.Select(r => new QuotationResultView(
                r.Id, r.InsurerId, r.Status, r.ErrorCategory,
                r.ErrorCode, r.ErrorMessageHuman,
                r.PremiumTotal, r.PremiumNet, r.Tax, r.Fees,
                r.LatencyMs, r.ExternalQuoteRef)).ToList());
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
