using McBrokers.Domain.Agents;

namespace McBrokers.Domain.Tests.Agents;

public class AgentTests
{
    private static readonly AgentEmail ValidEmail =
        AgentEmail.Create("user@mcbrokers.com.mx").Value;

    [Fact]
    public void Create_with_valid_inputs_succeeds()
    {
        var result = Agent.Create(ValidEmail, "Esteban Contreras", AgentRole.Agent);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be(ValidEmail);
        result.Value.FullName.Should().Be("Esteban Contreras");
        result.Value.Role.Should().Be(AgentRole.Agent);
        result.Value.IsActive.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_with_empty_full_name_fails(string? fullName)
    {
        var result = Agent.Create(ValidEmail, fullName!, AgentRole.Agent);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("name", because: "the error should explain what failed");
    }

    [Fact]
    public void Create_trims_full_name()
    {
        var result = Agent.Create(ValidEmail, "  Esteban Contreras  ", AgentRole.Agent);

        result.IsSuccess.Should().BeTrue();
        result.Value.FullName.Should().Be("Esteban Contreras");
    }

    [Fact]
    public void Deactivate_marks_agent_inactive()
    {
        var agent = Agent.Create(ValidEmail, "Esteban", AgentRole.Agent).Value;

        agent.Deactivate();

        agent.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Reactivate_marks_agent_active()
    {
        var agent = Agent.Create(ValidEmail, "Esteban", AgentRole.Agent).Value;
        agent.Deactivate();

        agent.Reactivate();

        agent.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ChangeRole_updates_role()
    {
        var agent = Agent.Create(ValidEmail, "Esteban", AgentRole.Agent).Value;

        agent.ChangeRole(AgentRole.Admin);

        agent.Role.Should().Be(AgentRole.Admin);
    }

    [Fact]
    public void UpdateFullName_changes_name_when_valid()
    {
        var agent = Agent.Create(ValidEmail, "Old Name", AgentRole.Agent).Value;

        var result = agent.UpdateFullName("New Name");

        result.IsSuccess.Should().BeTrue();
        agent.FullName.Should().Be("New Name");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateFullName_with_empty_value_fails_and_keeps_previous_name(string? newName)
    {
        var agent = Agent.Create(ValidEmail, "Old Name", AgentRole.Agent).Value;

        var result = agent.UpdateFullName(newName!);

        result.IsSuccess.Should().BeFalse();
        agent.FullName.Should().Be("Old Name");
    }

    [Fact]
    public void Two_agents_with_different_ids_are_not_equal()
    {
        var a = Agent.Create(ValidEmail, "Esteban", AgentRole.Agent).Value;
        var b = Agent.Create(ValidEmail, "Esteban", AgentRole.Agent).Value;

        a.Should().NotBe(b);
    }
}
