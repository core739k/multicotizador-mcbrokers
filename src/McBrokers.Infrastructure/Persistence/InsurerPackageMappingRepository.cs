using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Quotations;
using Microsoft.EntityFrameworkCore;

namespace McBrokers.Infrastructure.Persistence;

public class InsurerPackageMappingRepository : IInsurerPackageMappingRepository
{
    private readonly AppDbContext _db;
    public InsurerPackageMappingRepository(AppDbContext db) => _db = db;

    public async Task<string?> GetExternalCodeAsync(
        Guid insurerId, PackageCode internalPackage, CancellationToken cancellationToken)
    {
        var mapping = await _db.InsurerPackageMappings
            .SingleOrDefaultAsync(
                m => m.InsurerId == insurerId && m.InternalPackage == internalPackage,
                cancellationToken)
            .ConfigureAwait(false);
        return mapping?.ExternalCode;
    }

    public async Task<IReadOnlyList<InsurerPackageMapping>> ListByInsurerAsync(
        Guid insurerId, CancellationToken cancellationToken) =>
        await _db.InsurerPackageMappings
            .Where(m => m.InsurerId == insurerId)
            .OrderBy(m => m.InternalPackage)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<InsurerPackageMapping?> GetAsync(
        Guid insurerId, PackageCode internalPackage, CancellationToken cancellationToken) =>
        _db.InsurerPackageMappings.SingleOrDefaultAsync(
            m => m.InsurerId == insurerId && m.InternalPackage == internalPackage,
            cancellationToken);

    public async Task AddAsync(InsurerPackageMapping mapping, CancellationToken cancellationToken)
    {
        await _db.InsurerPackageMappings.AddAsync(mapping, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(InsurerPackageMapping mapping, CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);
}
