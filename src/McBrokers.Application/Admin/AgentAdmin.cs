using McBrokers.Application.Ports;
using McBrokers.Domain.Agents;
using McBrokers.SharedKernel;

namespace McBrokers.Application.Admin;

public sealed record UpdateAgentRoleCommand(Guid AgentId, AgentRole Role);

public sealed record SetAgentActiveCommand(Guid AgentId, bool IsActive);

public sealed record UpdateAgentCodeCommand(Guid AgentId, string? AgentCode);

public sealed record AgentView(Guid Id, string Email, string FullName, AgentRole Role, bool IsActive, string? AgentCode)
{
    public static AgentView From(Agent agent) =>
        new(agent.Id, agent.Email.Value, agent.FullName, agent.Role, agent.IsActive, agent.AgentCode);
}

public sealed class ListAgents
{
    private readonly IAgentRepository _agents;
    public ListAgents(IAgentRepository agents) => _agents = agents;

    public async Task<IReadOnlyList<AgentView>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var agents = await _agents.ListAsync(cancellationToken).ConfigureAwait(false);
        return agents
            .OrderBy(a => a.FullName)
            .Select(AgentView.From)
            .ToList();
    }
}

public sealed class UpdateAgentRole
{
    private readonly IAgentRepository _agents;
    private readonly IAuditWriter _audit;

    public UpdateAgentRole(IAgentRepository agents, IAuditWriter audit)
    {
        _agents = agents;
        _audit = audit;
    }

    public async Task<Result<Guid>> ExecuteAsync(UpdateAgentRoleCommand command, CancellationToken cancellationToken)
    {
        var agent = await _agents.GetByIdAsync(command.AgentId, cancellationToken).ConfigureAwait(false);
        if (agent is null)
        {
            return Result<Guid>.Failure($"Agent with id '{command.AgentId}' not found.");
        }

        var previous = agent.Role;
        agent.ChangeRole(command.Role);

        await _agents.UpdateAsync(agent, cancellationToken).ConfigureAwait(false);
        await _audit.WriteAsync(
            action: "Agent.UpdateRole",
            entityType: "Agent",
            entityId: agent.Id.ToString(),
            payload: new { agent.Email.Value, From = previous, To = agent.Role },
            cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(agent.Id);
    }
}

public sealed class SetAgentActive
{
    private readonly IAgentRepository _agents;
    private readonly IAuditWriter _audit;

    public SetAgentActive(IAgentRepository agents, IAuditWriter audit)
    {
        _agents = agents;
        _audit = audit;
    }

    public async Task<Result<Guid>> ExecuteAsync(SetAgentActiveCommand command, CancellationToken cancellationToken)
    {
        var agent = await _agents.GetByIdAsync(command.AgentId, cancellationToken).ConfigureAwait(false);
        if (agent is null)
        {
            return Result<Guid>.Failure($"Agent with id '{command.AgentId}' not found.");
        }

        if (command.IsActive) agent.Reactivate();
        else agent.Deactivate();

        await _agents.UpdateAsync(agent, cancellationToken).ConfigureAwait(false);
        await _audit.WriteAsync(
            action: command.IsActive ? "Agent.Reactivate" : "Agent.Deactivate",
            entityType: "Agent",
            entityId: agent.Id.ToString(),
            payload: new { agent.Email.Value, agent.IsActive },
            cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(agent.Id);
    }
}

public sealed class UpdateAgentCode
{
    private readonly IAgentRepository _agents;
    private readonly IAuditWriter _audit;

    public UpdateAgentCode(IAgentRepository agents, IAuditWriter audit)
    {
        _agents = agents;
        _audit = audit;
    }

    public async Task<Result<Guid>> ExecuteAsync(UpdateAgentCodeCommand command, CancellationToken cancellationToken)
    {
        var agent = await _agents.GetByIdAsync(command.AgentId, cancellationToken).ConfigureAwait(false);
        if (agent is null)
        {
            return Result<Guid>.Failure($"Agent with id '{command.AgentId}' not found.");
        }

        var previous = agent.AgentCode;
        var update = agent.SetAgentCode(command.AgentCode);
        if (!update.IsSuccess) return Result<Guid>.Failure(update.Error);

        await _agents.UpdateAsync(agent, cancellationToken).ConfigureAwait(false);
        await _audit.WriteAsync(
            action: "Agent.UpdateAgentCode",
            entityType: "Agent",
            entityId: agent.Id.ToString(),
            payload: new { agent.Email.Value, From = previous, To = agent.AgentCode },
            cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(agent.Id);
    }
}
