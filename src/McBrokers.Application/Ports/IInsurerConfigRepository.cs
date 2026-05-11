using McBrokers.Domain.Insurers;

namespace McBrokers.Application.Ports;

public interface IInsurerConfigRepository
{
    Task<InsurerConfig?> GetAsync(Guid insurerId, InsurerEnvironment environment, CancellationToken cancellationToken);

    Task<IReadOnlyList<InsurerConfig>> ListByInsurerAsync(Guid insurerId, CancellationToken cancellationToken);

    Task AddAsync(InsurerConfig config, CancellationToken cancellationToken);

    Task UpdateAsync(InsurerConfig config, CancellationToken cancellationToken);
}
