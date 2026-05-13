using McBrokers.Application.Catalog;
using McBrokers.Application.Ports;
using McBrokers.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace McBrokers.Infrastructure.Persistence;

// Repositorio del fallback de búsqueda libre del wizard de cotización.
// LIKE multi-token (AND entre tokens, OR entre campos Brand/Model/Version)
// + join con VehicleInsurerMapping Approved restringido a las aseguradoras
// seleccionadas (filtro permisivo: basta una para que el vehículo aparezca).
public class VehicleSearchRepository : ISearchVehiclesByTextRepository
{
    private const int MaxResults = 50;

    private readonly AppDbContext _db;
    public VehicleSearchRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<VehicleSearchHit>> SearchAsync(
        VehicleSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        if (criteria.Tokens.Count == 0 || criteria.InsurerIds.Count == 0)
        {
            return Array.Empty<VehicleSearchHit>();
        }

        var insurerIds = criteria.InsurerIds.ToList();

        IQueryable<VehicleMaster> masters = _db.VehicleMasters
            .AsNoTracking()
            .Where(v => v.Year == criteria.Year && v.IsActive);

        // AND multi-token: cada token debe aparecer en alguna de las 3 columnas.
        foreach (var token in criteria.Tokens)
        {
            var pattern = $"%{token}%";
            masters = masters.Where(v =>
                EF.Functions.Like(v.Brand, pattern) ||
                EF.Functions.Like(v.Model, pattern) ||
                EF.Functions.Like(v.Version, pattern));
        }

        var joined =
            from m in masters
            join mp in _db.VehicleInsurerMappings.AsNoTracking()
                .Where(x => x.ReviewState == ReviewState.Approved
                            && insurerIds.Contains(x.InsurerId))
                on m.Id equals mp.VehicleMasterId
            select new { Master = m, mp.InsurerId };

        var grouped = await joined
            .GroupBy(x => new
            {
                x.Master.Id,
                x.Master.Year,
                x.Master.Brand,
                x.Master.Model,
                x.Master.Version,
            })
            .Select(g => new
            {
                g.Key.Id,
                g.Key.Year,
                g.Key.Brand,
                g.Key.Model,
                g.Key.Version,
                InsurerIds = g.Select(x => x.InsurerId).Distinct().ToList(),
            })
            .OrderBy(x => x.Brand).ThenBy(x => x.Model).ThenBy(x => x.Version)
            .Take(MaxResults)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return grouped
            .Select(g => new VehicleSearchHit(g.Id, g.Year, g.Brand, g.Model, g.Version, g.InsurerIds))
            .ToList();
    }
}
