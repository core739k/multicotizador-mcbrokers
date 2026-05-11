using McBrokers.Application.Ports;
using McBrokers.Domain.Agents;
using McBrokers.SharedKernel;

namespace McBrokers.Application.Auth;

public sealed class ResolveAgentFromGoogleToken
{
    private readonly IAgentRepository _agents;

    public ResolveAgentFromGoogleToken(IAgentRepository agents) => _agents = agents;

    public async Task<Result<Agent>> ResolveAsync(GoogleIdentity identity, CancellationToken cancellationToken)
    {
        var emailResult = AgentEmail.Create(identity.Email);
        if (!emailResult.IsSuccess)
        {
            return Result<Agent>.Failure(emailResult.Error);
        }

        var email = emailResult.Value;

        var existing = await _agents.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.IsActive
                ? Result<Agent>.Success(existing)
                : Result<Agent>.Failure($"Agent '{email}' is inactive.");
        }

        var fullName = string.IsNullOrWhiteSpace(identity.FullName)
            ? email.Value
            : identity.FullName;

        var creation = Agent.Create(email, fullName, AgentRole.Agent);
        if (!creation.IsSuccess)
        {
            return creation;
        }

        await _agents.AddAsync(creation.Value, cancellationToken).ConfigureAwait(false);
        return creation;
    }
}
