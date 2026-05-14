using McBrokers.Application.Ports;
using McBrokers.Domain.Catalog;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Quotations;

namespace McBrokers.Application.Admin;

public sealed record QuotationListItem(
    Guid Id,
    string CorrelationId,
    DateTime CreatedAt,
    QuotationStatus Status,
    int ExpectedResultsCount,
    int CurrentResultsCount,
    string VehicleSummary,
    Guid AgentId);

public sealed record QuotationsPage(
    IReadOnlyList<QuotationListItem> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record QuotationAdminDetailView(
    Guid Id,
    string CorrelationId,
    DateTime CreatedAt,
    QuotationStatus Status,
    int ExpectedResultsCount,
    Guid AgentId,
    string PostalCode,
    string VehicleSummary,
    IReadOnlyList<QuotationResultBlobRefs> Results);

public sealed record QuotationResultBlobRefs(
    Guid ResultId,
    Guid InsurerId,
    string? InsurerName,
    InsurerCode? InsurerCode,
    int Version,
    bool IsCurrent,
    QuotationInsurerStatus Status,
    string? ErrorCode,
    string? ErrorMessage,
    string? RequestBlobRef,
    string? ResponseBlobRef);

public sealed class ListRecentQuotations
{
    private readonly IQuotationRepository _quotations;
    private readonly IVehicleMasterRepository _vehicles;

    public ListRecentQuotations(IQuotationRepository quotations, IVehicleMasterRepository vehicles)
    {
        _quotations = quotations;
        _vehicles = vehicles;
    }

    public async Task<QuotationsPage> ExecuteAsync(int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var skip = (page - 1) * pageSize;
        var quotations = await _quotations.ListRecentAsync(pageSize, skip, ct).ConfigureAwait(false);
        var total = await _quotations.CountAsync(ct).ConfigureAwait(false);

        var items = new List<QuotationListItem>(quotations.Count);
        foreach (var q in quotations)
        {
            var vehicle = await _vehicles.GetByIdAsync(q.VehicleMasterId, ct).ConfigureAwait(false);
            var summary = vehicle is null
                ? "(vehículo no disponible)"
                : $"{vehicle.Year} {vehicle.Brand} {vehicle.Model} — {vehicle.Version}";

            // ListRecentAsync no hidrata Results — necesitamos un GetByIdAsync por
            // cada Quotation para tener IsCurrent. Para la pantalla admin (10-25 filas
            // por página) el overhead es aceptable. Si crece el catálogo, mover esto
            // a una query con JOIN.
            var hydrated = await _quotations.GetByIdAsync(q.Id, ct).ConfigureAwait(false);
            var currentResultsCount = hydrated?.Results.Count(r => r.IsCurrent) ?? 0;

            items.Add(new QuotationListItem(
                q.Id, q.CorrelationId, q.CreatedAt,
                hydrated?.Status ?? q.Status, q.ExpectedResultsCount,
                currentResultsCount, summary, q.AgentId));
        }

        return new QuotationsPage(items, total, page, pageSize);
    }
}

public sealed class GetQuotationAdminDetail
{
    private readonly IQuotationRepository _quotations;
    private readonly IVehicleMasterRepository _vehicles;
    private readonly IInsurerRepository _insurers;

    public GetQuotationAdminDetail(
        IQuotationRepository quotations,
        IVehicleMasterRepository vehicles,
        IInsurerRepository insurers)
    {
        _quotations = quotations;
        _vehicles = vehicles;
        _insurers = insurers;
    }

    public async Task<QuotationAdminDetailView?> ExecuteAsync(Guid id, CancellationToken ct)
    {
        var q = await _quotations.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (q is null) return null;

        var vehicle = await _vehicles.GetByIdAsync(q.VehicleMasterId, ct).ConfigureAwait(false);
        var summary = vehicle is null
            ? "(vehículo no disponible)"
            : $"{vehicle.Year} {vehicle.Brand} {vehicle.Model} — {vehicle.Version}";

        var insurers = (await _insurers.ListAsync(ct).ConfigureAwait(false))
            .ToDictionary(i => i.Id);

        // Incluye TODOS los results (current + superseded) para auditoría —
        // el admin quiere ver el historial de re-cotizaciones.
        var results = q.Results
            .OrderByDescending(r => r.CreatedAt)
            .Select(r =>
            {
                var ins = insurers.GetValueOrDefault(r.InsurerId);
                return new QuotationResultBlobRefs(
                    r.Id, r.InsurerId, ins?.Name, ins?.Code,
                    r.Version, r.IsCurrent, r.Status,
                    r.ErrorCode, r.ErrorMessageHuman,
                    r.RequestBlobRef, r.ResponseBlobRef);
            })
            .ToList();

        return new QuotationAdminDetailView(
            q.Id, q.CorrelationId, q.CreatedAt, q.Status, q.ExpectedResultsCount,
            q.AgentId, q.PostalCode, summary, results);
    }
}
