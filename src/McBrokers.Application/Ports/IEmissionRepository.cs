using McBrokers.Domain.Emissions;

namespace McBrokers.Application.Ports;

public interface IEmissionRepository
{
    Task<Emission?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Emission?> GetByQuotationResultAsync(Guid quotationInsurerResultId, CancellationToken cancellationToken);

    Task AddAsync(Emission emission, CancellationToken cancellationToken);

    Task UpdateAsync(Emission emission, CancellationToken cancellationToken);

    Task AddAttemptAsync(EmissionAttempt attempt, CancellationToken cancellationToken);
}
