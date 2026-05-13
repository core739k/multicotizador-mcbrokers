using System.Text.RegularExpressions;
using McBrokers.SharedKernel;

namespace McBrokers.Domain.Agents;

public sealed class Agent
{
    public const int AgentCodeMinLength = 3;
    public const int AgentCodeMaxLength = 15;

    // Clave interna MCBrokers para comisiones. Alfanumérica con guiones opcionales.
    // Distinta de AgentInsurerKey.ExternalAgentCode (clave por aseguradora).
    private static readonly Regex AgentCodePattern = new(
        $"^[A-Za-z0-9-]{{{AgentCodeMinLength},{AgentCodeMaxLength}}}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public Guid Id { get; }
    public AgentEmail Email { get; }
    public string FullName { get; private set; }
    public AgentRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsTechnical { get; private set; }
    public string? AgentCode { get; private set; }

    private Agent(Guid id, AgentEmail email, string fullName, AgentRole role, bool isActive, bool isTechnical, string? agentCode)
    {
        Id = id;
        Email = email;
        FullName = fullName;
        Role = role;
        IsActive = isActive;
        IsTechnical = isTechnical;
        AgentCode = agentCode;
    }

    public static Result<Agent> Create(AgentEmail email, string fullName, AgentRole role, string? agentCode = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result<Agent>.Failure("Full name must not be empty.");
        }

        string? normalizedCode = null;
        if (!string.IsNullOrWhiteSpace(agentCode))
        {
            var validation = ValidateAgentCode(agentCode);
            if (!validation.IsSuccess) return Result<Agent>.Failure(validation.Error);
            normalizedCode = validation.Value;
        }

        return Result<Agent>.Success(new Agent(
            Guid.NewGuid(),
            email,
            fullName.Trim(),
            role,
            isActive: true,
            isTechnical: false,
            agentCode: normalizedCode));
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

    public Result<Agent> SetAgentCode(string? agentCode)
    {
        if (string.IsNullOrWhiteSpace(agentCode))
        {
            AgentCode = null;
            return Result<Agent>.Success(this);
        }

        var validation = ValidateAgentCode(agentCode);
        if (!validation.IsSuccess) return Result<Agent>.Failure(validation.Error);

        AgentCode = validation.Value;
        return Result<Agent>.Success(this);
    }

    private static Result<string> ValidateAgentCode(string agentCode)
    {
        var trimmed = agentCode.Trim();
        if (!AgentCodePattern.IsMatch(trimmed))
        {
            return Result<string>.Failure(
                $"Agent code must be {AgentCodeMinLength}-{AgentCodeMaxLength} alphanumeric characters (dashes allowed).");
        }
        return Result<string>.Success(trimmed);
    }
}
