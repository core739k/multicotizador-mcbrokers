using McBrokers.Domain.Agents;

namespace McBrokers.Application.Ports;

public interface IAgentRepository
{
    Task<Agent?> GetByEmailAsync(AgentEmail email, CancellationToken cancellationToken);

    Task AddAsync(Agent agent, CancellationToken cancellationToken);
}
