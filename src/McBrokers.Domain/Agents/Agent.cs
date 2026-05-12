using McBrokers.SharedKernel;

namespace McBrokers.Domain.Agents;

public sealed class Agent
{
    public Guid Id { get; }
    public AgentEmail Email { get; }
    public string FullName { get; private set; }
    public AgentRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsTechnical { get; private set; }

    private Agent(Guid id, AgentEmail email, string fullName, AgentRole role, bool isActive, bool isTechnical)
    {
        Id = id;
        Email = email;
        FullName = fullName;
        Role = role;
        IsActive = isActive;
        IsTechnical = isTechnical;
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
            isActive: true,
            isTechnical: false));
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    public void ChangeRole(AgentRole role) => Role = role;

    public Result<Agent> MakeTechnical()
    {
        if (Role != AgentRole.Admin)
        {
            return Result<Agent>.Failure("Only Admin agents can be granted the technical flag.");
        }

        IsTechnical = true;
        return Result<Agent>.Success(this);
    }

    public void RevokeTechnical() => IsTechnical = false;

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
