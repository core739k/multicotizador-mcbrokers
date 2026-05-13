using McBrokers.Application.Ports;
using McBrokers.SharedKernel;

namespace McBrokers.Application.Agents;

public sealed record CurrentAgentView(string FullName, string? AgentCode, string? PhotoUrl);

public sealed class GetCurrentAgentSummary
{
    private readonly IAgentRepository _agents;
    private readonly ICurrentAgentProvider _current;

    public GetCurrentAgentSummary(IAgentRepository agents, ICurrentAgentProvider current)
    {
        _agents = agents;
        _current = current;
    }

    public async Task<Result<CurrentAgentView>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var id = _current.AgentId;
        var agent = await _agents.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (agent is null)
        {
            return Result<CurrentAgentView>.Failure($"Current agent '{id}' not found.");
        }

        return Result<CurrentAgentView>.Success(
            new CurrentAgentView(agent.FullName, agent.AgentCode, PhotoUrl: null));
    }
}
