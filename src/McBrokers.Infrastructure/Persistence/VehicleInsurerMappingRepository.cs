using McBrokers.Application.Ports;
using McBrokers.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace McBrokers.Infrastructure.Persistence;

public class VehicleInsurerMappingRepository : IVehicleInsurerMappingRepository
{
    private readonly AppDbContext _db;
    public VehicleInsurerMappingRepository(AppDbContext db) => _db = db;

    public Task<VehicleInsurerMapping?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.VehicleInsurerMappings.SingleOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<VehicleInsurerMapping?> FindByInsurerAndExternalClaveAsync(
        Guid insurerId, string externalClave, CancellationToken cancellationToken) =>
        _db.VehicleInsurerMappings.SingleOrDefaultAsync(
            m => m.InsurerId == insurerId && m.ExternalClave == externalClave,
            cancellationToken);

    public async Task<IReadOnlyList<VehicleInsurerMapping>> ListByMasterAsync(
        Guid vehicleMasterId, CancellationToken cancellationToken) =>
        await _db.VehicleInsurerMappings
            .Where(m => m.VehicleMasterId == vehicleMasterId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<VehicleInsurerMapping>> ListPendingAsync(
        int take, int skip, CancellationToken cancellationToken) =>
        await _db.VehicleInsurerMappings
            .Where(m => m.ReviewState == ReviewState.Pending)
            .OrderBy(m => m.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<int> CountPendingAsync(CancellationToken cancellationToken) =>
        _db.VehicleInsurerMappings.CountAsync(m => m.ReviewState == ReviewState.Pending, cancellationToken);

    public async Task AddAsync(VehicleInsurerMapping mapping, CancellationToken cancellationToken)
    {
        await _db.VehicleInsurerMappings.AddAsync(mapping, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(VehicleInsurerMapping mapping, CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);
}
