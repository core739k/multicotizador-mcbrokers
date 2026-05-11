using McBrokers.Domain.Catalog;

namespace McBrokers.Application.Ports;

public interface IVehicleInsurerMappingRepository
{
    Task<VehicleInsurerMapping?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<VehicleInsurerMapping?> FindByInsurerAndExternalClaveAsync(
        Guid insurerId, string externalClave, CancellationToken cancellationToken);

    Task<IReadOnlyList<VehicleInsurerMapping>> ListByMasterAsync(Guid vehicleMasterId, CancellationToken cancellationToken);

    Task<IReadOnlyList<VehicleInsurerMapping>> ListPendingAsync(int take, int skip, CancellationToken cancellationToken);

    Task AddAsync(VehicleInsurerMapping mapping, CancellationToken cancellationToken);

    Task UpdateAsync(VehicleInsurerMapping mapping, CancellationToken cancellationToken);

    Task<int> CountPendingAsync(CancellationToken cancellationToken);
}
