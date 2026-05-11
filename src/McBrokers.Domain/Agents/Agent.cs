using McBrokers.SharedKernel;

namespace McBrokers.Domain.Agents;

public sealed class Agent
{
    public Guid Id { get; }
    public AgentEmail Email { get; }
    public string FullName { get; private set; }
    public AgentRole Role { get; private set; }
    public bool IsActive { get; private set; }

    private Agent(Guid id, AgentEmail email, string fullName, AgentRole role, bool isActive)
    {
        Id = id;
        Email = email;
        FullName = fullName;
        Role = role;
        IsActive = isActive;
    }

    public static Result<Agent> Create(AgentEmail email, string fullName, AgentRole role)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result<Agent>.Failure("Full name must not be empty.");
        }

        return Result<Agent>.Success(new Agent(
            Guid.NewGuid(),
            email,
            fullName.Trim(),
            role,
            isActive: true));
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    public void ChangeRole(AgentRole role) => Role = role;

    public Result<Agent> UpdateFullName(string newFullName)
    {
        if (string.IsNullOrWhiteSpace(newFullName))
        {
            return Result<Agent>.Failure("Full name must not be empty.");
        }

        FullName = newFullName.Trim();
        return Result<Agent>.Success(this);
    }
}
