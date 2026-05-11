using McBrokers.Application.Ports;
using McBrokers.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace McBrokers.Infrastructure.Persistence;

public class VehicleMasterRepository : IVehicleMasterRepository
{
    private readonly AppDbContext _db;
    public VehicleMasterRepository(AppDbContext db) => _db = db;

    public Task<VehicleMaster?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.VehicleMasters.SingleOrDefaultAsync(v => v.Id == id, cancellationToken);

    public async Task<IReadOnlyList<VehicleMaster>> FindByYearAndBrandAsync(
        int year, string normalizedBrand, CancellationToken cancellationToken) =>
        await _db.VehicleMasters
            .Where(v => v.Year == year && v.Brand.ToUpper() == normalizedBrand)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<VehicleMaster>> ListByYearAsync(int year, CancellationToken cancellationToken) =>
        await _db.VehicleMasters
            .Where(v => v.Year == year && v.IsActive)
            .OrderBy(v => v.Brand)
            .ThenBy(v => v.Model)
            .ThenBy(v => v.Version)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AddAsync(VehicleMaster master, CancellationToken cancellationToken)
    {
        await _db.VehicleMasters.AddAsync(master, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
