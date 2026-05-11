namespace McBrokers.Application.Ports;

public interface ICurrentAgentProvider
{
    Guid AgentId { get; }
}
