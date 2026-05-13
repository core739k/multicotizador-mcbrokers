using McBrokers.Application.Catalog;

namespace McBrokers.Application.Ports;

public interface ISearchVehiclesByTextRepository
{
    Task<IReadOnlyList<VehicleSearchHit>> SearchAsync(VehicleSearchCriteria criteria, CancellationToken cancellationToken);
}
