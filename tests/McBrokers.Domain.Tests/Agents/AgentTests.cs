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
        result.Value.IsTechnical.Should().BeFalse(because: "newly created agents are never technical by default");
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
    public void MakeTechnical_succeeds_when_agent_is_admin()
    {
        var agent = Agent.Create(ValidEmail, "Esteban", AgentRole.Admin).Value;

        var result = agent.MakeTechnical();

        result.IsSuccess.Should().BeTrue();
        agent.IsTechnical.Should().BeTrue();
    }

    [Fact]
    public void MakeTechnical_fails_when_agent_is_not_admin()
    {
        var agent = Agent.Create(ValidEmail, "Esteban", AgentRole.Agent).Value;

        var result = agent.MakeTechnical();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Admin",
            because: "the technical flag is gated on the Admin role to prevent privilege escalation");
        agent.IsTechnical.Should().BeFalse();
    }

    [Fact]
    public void MakeTechnical_fails_when_agent_is_finance()
    {
        var agent = Agent.Create(ValidEmail, "Esteban", AgentRole.Finance).Value;

        var result = agent.MakeTechnical();

        result.IsSuccess.Should().BeFalse();
        agent.IsTechnical.Should().BeFalse();
    }

    [Fact]
    public void RevokeTechnical_clears_the_flag()
    {
        var agent = Agent.Create(ValidEmail, "Esteban", AgentRole.Admin).Value;
        agent.MakeTechnical();

        agent.RevokeTechnical();

        agent.IsTechnical.Should().BeFalse();
    }

    [Fact]
    public void RevokeTechnical_is_idempotent_when_already_not_technical()
    {
        var agent = Agent.Create(ValidEmail, "Esteban", AgentRole.Agent).Value;

        agent.RevokeTechnical();

        agent.IsTechnical.Should().BeFalse();
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

    [Fact]
    public void Newly_created_agent_has_no_agent_code()
    {
        var agent = Agent.Create(ValidEmail, "Esteban", AgentRole.Agent).Value;

        agent.AgentCode.Should().BeNull(
            because: "OAuth auto-provisions agents without a commission code; admin captures it later");
    }

    [Theory]
    [InlineData("MCB001")]
    [InlineData("MCB-001")]
    [InlineData("ECONT01")]
    [InlineData("ABC")]
    [InlineData("123456789012345")]
    public void SetAgentCode_accepts_valid_codes(string code)
    {
        var agent = Agent.Create(ValidEmail, "Esteban", AgentRole.Agent).Value;

        var result = agent.SetAgentCode(code);

        result.IsSuccess.Should().BeTrue();
        agent.AgentCode.Should().Be(code);
    }

    [Fact]
    public void SetAgentCode_trims_whitespace()
    {
        var agent = Agent.Create(ValidEmail, "Esteban", AgentRole.Agent).Value;

        var result = agent.SetAgentCode("  MCB-001  ");

        result.IsSuccess.Should().BeTrue();
        agent.AgentCode.Should().Be("MCB-001");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetAgentCode_with_blank_value_clears_the_code(string? blank)
    {
        var agent = Agent.Create(ValidEmail, "Esteban", AgentRole.Agent).Value;
        agent.SetAgentCode("MCB-001");

        var result = agent.SetAgentCode(blank);

        result.IsSuccess.Should().BeTrue();
        agent.AgentCode.Should().BeNull();
    }

    [Theory]
    [InlineData("AB")]
    [InlineData("1234567890123456")]
    public void SetAgentCode_rejects_codes_outside_length_bounds(string code)
    {
        var agent = Agent.Create(ValidEmail, "Esteban", AgentRole.Agent).Value;
        agent.SetAgentCode("PREV-01");

        var result = agent.SetAgentCode(code);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("3", because: "the error should mention the length bounds");
        agent.AgentCode.Should().Be("PREV-01", because: "rejected updates leave previous value intact");
    }

    [Theory]
    [InlineData("MCB 001")]
    [InlineData("MCB/001")]
    [InlineData("MCB_001")]
    [InlineData("MCB.001")]
    public void SetAgentCode_rejects_codes_with_disallowed_characters(string code)
    {
        var agent = Agent.Create(ValidEmail, "Esteban", AgentRole.Agent).Value;

        var result = agent.SetAgentCode(code);

        result.IsSuccess.Should().BeFalse();
        agent.AgentCode.Should().BeNull();
    }

    [Fact]
    public void Create_can_seed_a_valid_agent_code()
    {
        var result = Agent.Create(ValidEmail, "Esteban", AgentRole.Agent, agentCode: "MCB-001");

        result.IsSuccess.Should().BeTrue();
        result.Value.AgentCode.Should().Be("MCB-001");
    }

    [Fact]
    public void Create_rejects_invalid_agent_code()
    {
        var result = Agent.Create(ValidEmail, "Esteban", AgentRole.Agent, agentCode: "AB");

        result.IsSuccess.Should().BeFalse();
    }
}
