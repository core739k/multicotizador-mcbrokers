using McBrokers.Domain.Insurers;

namespace McBrokers.Application.Ports;

public interface IInsurerConfigRepository
{
    Task<InsurerConfig?> GetAsync(Guid insurerId, CancellationToken cancellationToken);

    Task AddAsync(InsurerConfig config, CancellationToken cancellationToken);

    Task UpdateAsync(InsurerConfig config, CancellationToken cancellationToken);
}
