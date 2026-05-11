using McBrokers.Domain.Catalog;

namespace McBrokers.Application.Ports;

public interface IVehicleMasterRepository
{
    Task<VehicleMaster?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<VehicleMaster>> FindByYearAndBrandAsync(int year, string normalizedBrand, CancellationToken cancellationToken);

    Task<IReadOnlyList<VehicleMaster>> ListByYearAsync(int year, CancellationToken cancellationToken);

    Task AddAsync(VehicleMaster master, CancellationToken cancellationToken);
}
