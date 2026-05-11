using McBrokers.Application.Ports;
using McBrokers.Domain.Catalog;

namespace McBrokers.Application.Catalog;

public sealed record VehicleMasterView(
    Guid Id, int Year, string Brand, string Model, string Version,
    string BodyType, VehicleTransmission Transmission, int Doors, int Cylinders, bool IsActive)
{
    public static VehicleMasterView From(VehicleMaster v) =>
        new(v.Id, v.Year, v.Brand, v.Model, v.Version, v.BodyType, v.Transmission, v.Doors, v.Cylinders, v.IsActive);
}

public sealed record InsurerMappingView(
    Guid Id, Guid VehicleMasterId, Guid InsurerId,
    string ExternalClave, string InsurerBrandRaw, string InsurerModelRaw, string InsurerVersionRaw,
    decimal ConfidenceScore, ReviewState ReviewState)
{
    public static InsurerMappingView From(VehicleInsurerMapping m) =>
        new(m.Id, m.VehicleMasterId, m.InsurerId, m.ExternalClave,
            m.InsurerBrandRaw, m.InsurerModelRaw, m.InsurerVersionRaw,
            m.ConfidenceScore, m.ReviewState);
}

public sealed record CatalogYearView(
    int Year,
    IReadOnlyList<VehicleMasterView> Vehicles,
    IReadOnlyList<InsurerMappingView> ApprovedMappings);

public sealed class GetCatalogForYear
{
    private readonly IVehicleMasterRepository _masters;
    private readonly IVehicleInsurerMappingRepository _mappings;

    public GetCatalogForYear(IVehicleMasterRepository masters, IVehicleInsurerMappingRepository mappings)
    {
        _masters = masters;
        _mappings = mappings;
    }

    public async Task<CatalogYearView> ExecuteAsync(int year, CancellationToken cancellationToken)
    {
        var masters = await _masters.ListByYearAsync(year, cancellationToken).ConfigureAwait(false);
        var approvedMappings = new List<InsurerMappingView>();

        foreach (var m in masters)
        {
            var mappings = await _mappings.ListByMasterAsync(m.Id, cancellationToken).ConfigureAwait(false);
            approvedMappings.AddRange(
                mappings.Where(x => x.ReviewState == ReviewState.Approved)
                        .Select(InsurerMappingView.From));
        }

        return new CatalogYearView(
            year,
            masters.Select(VehicleMasterView.From).ToList(),
            approvedMappings);
    }
}

public sealed class ListPendingMappings
{
    private readonly IVehicleInsurerMappingRepository _mappings;
    private readonly IVehicleMasterRepository _masters;

    public ListPendingMappings(IVehicleInsurerMappingRepository mappings, IVehicleMasterRepository masters)
    {
        _mappings = mappings;
        _masters = masters;
    }

    public async Task<PendingMappingsPage> ExecuteAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var skip = Math.Max(0, (page - 1) * pageSize);
        var pending = await _mappings.ListPendingAsync(pageSize, skip, cancellationToken).ConfigureAwait(false);
        var total = await _mappings.CountPendingAsync(cancellationToken).ConfigureAwait(false);

        var items = new List<PendingMappingItem>();
        foreach (var p in pending)
        {
            var master = await _masters.GetByIdAsync(p.VehicleMasterId, cancellationToken).ConfigureAwait(false);
            if (master is null) continue;
            items.Add(new PendingMappingItem(InsurerMappingView.From(p), VehicleMasterView.From(master)));
        }

        return new PendingMappingsPage(items, total, page, pageSize);
    }
}

public sealed record PendingMappingItem(InsurerMappingView Mapping, VehicleMasterView CandidateMaster);

public sealed record PendingMappingsPage(IReadOnlyList<PendingMappingItem> Items, int Total, int Page, int PageSize);
