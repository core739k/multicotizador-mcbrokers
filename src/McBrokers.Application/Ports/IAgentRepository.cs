using McBrokers.Domain.Agents;

namespace McBrokers.Application.Ports;

public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Agent?> GetByEmailAsync(AgentEmail email, CancellationToken cancellationToken);

    Task<IReadOnlyList<Agent>> ListAsync(CancellationToken cancellationToken);

    Task AddAsync(Agent agent, CancellationToken cancellationToken);

    Task UpdateAsync(Agent agent, CancellationToken cancellationToken);
}
