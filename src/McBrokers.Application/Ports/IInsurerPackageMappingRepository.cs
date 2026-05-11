using McBrokers.Domain.Insurers;
using McBrokers.Domain.Quotations;

namespace McBrokers.Application.Ports;

public interface IInsurerPackageMappingRepository
{
    Task<string?> GetExternalCodeAsync(
        Guid insurerId, PackageCode internalPackage, CancellationToken cancellationToken);

    Task<IReadOnlyList<InsurerPackageMapping>> ListByInsurerAsync(
        Guid insurerId, CancellationToken cancellationToken);

    Task<InsurerPackageMapping?> GetAsync(
        Guid insurerId, PackageCode internalPackage, CancellationToken cancellationToken);

    Task AddAsync(InsurerPackageMapping mapping, CancellationToken cancellationToken);

    Task UpdateAsync(InsurerPackageMapping mapping, CancellationToken cancellationToken);
}
