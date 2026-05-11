using McBrokers.Domain.Insurers;

namespace McBrokers.Application.Ports;

public interface IInsurerRepository
{
    Task<Insurer?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Insurer?> GetByCodeAsync(InsurerCode code, CancellationToken cancellationToken);

    Task<IReadOnlyList<Insurer>> ListAsync(CancellationToken cancellationToken);

    Task AddAsync(Insurer insurer, CancellationToken cancellationToken);

    Task UpdateAsync(Insurer insurer, CancellationToken cancellationToken);
}
