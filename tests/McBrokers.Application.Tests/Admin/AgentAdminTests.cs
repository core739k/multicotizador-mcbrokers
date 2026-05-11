using McBrokers.Application.Admin;
using McBrokers.Application.Ports;
using McBrokers.Domain.Agents;

namespace McBrokers.Application.Tests.Admin;

public class AgentAdminTests
{
    private readonly Mock<IAgentRepository> _agents = new(MockBehavior.Strict);
    private readonly Mock<IAuditWriter> _audit = new(MockBehavior.Strict);

    private static Agent BuildAgent(AgentRole role = AgentRole.Agent, bool active = true)
    {
        var agent = Agent.Create(AgentEmail.Create("user@mcbrokers.com.mx").Value, "User", role).Value;
        if (!active) agent.Deactivate();
        return agent;
    }

    [Fact]
    public async Task UpdateAgentRole_changes_role_and_audits()
    {
        var agent = BuildAgent();
        _agents.Setup(r => r.GetByIdAsync(agent.Id, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        _agents.Setup(r => r.UpdateAsync(agent, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _audit.Setup(a => a.WriteAsync(
            "Agent.UpdateRole", "Agent", agent.Id.ToString(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new UpdateAgentRole(_agents.Object, _audit.Object);
        var result = await handler.ExecuteAsync(
            new UpdateAgentRoleCommand(agent.Id, AgentRole.Admin),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        agent.Role.Should().Be(AgentRole.Admin);
    }

    [Fact]
    public async Task UpdateAgentRole_fails_when_agent_not_found()
    {
        var id = Guid.NewGuid();
        _agents.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Agent?)null);

        var handler = new UpdateAgentRole(_agents.Object, _audit.Object);
        var result = await handler.ExecuteAsync(
            new UpdateAgentRoleCommand(id, AgentRole.Admin),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task SetAgentActive_deactivates_and_audits()
    {
        var agent = BuildAgent(active: true);
        _agents.Setup(r => r.GetByIdAsync(agent.Id, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        _agents.Setup(r => r.UpdateAsync(agent, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _audit.Setup(a => a.WriteAsync(
            "Agent.Deactivate", "Agent", agent.Id.ToString(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new SetAgentActive(_agents.Object, _audit.Object);
        var result = await handler.ExecuteAsync(
            new SetAgentActiveCommand(agent.Id, IsActive: false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        agent.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SetAgentActive_reactivates_and_audits()
    {
        var agent = BuildAgent(active: false);
        _agents.Setup(r => r.GetByIdAsync(agent.Id, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        _agents.Setup(r => r.UpdateAsync(agent, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _audit.Setup(a => a.WriteAsync(
            "Agent.Reactivate", "Agent", agent.Id.ToString(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new SetAgentActive(_agents.Object, _audit.Object);
        var result = await handler.ExecuteAsync(
            new SetAgentActiveCommand(agent.Id, IsActive: true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        agent.IsActive.Should().BeTrue();
    }
}
