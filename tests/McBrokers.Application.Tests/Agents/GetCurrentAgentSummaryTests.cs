using McBrokers.Application.Agents;
using McBrokers.Application.Ports;
using McBrokers.Domain.Agents;

namespace McBrokers.Application.Tests.Agents;

public class GetCurrentAgentSummaryTests
{
    private readonly Mock<IAgentRepository> _agents = new(MockBehavior.Strict);
    private readonly Mock<ICurrentAgentProvider> _current = new(MockBehavior.Strict);

    private static Agent BuildAgent(string? agentCode = null)
    {
        var email = AgentEmail.Create("user@mcbrokers.com.mx").Value;
        return Agent.Create(email, "Esteban Contreras", AgentRole.Agent, agentCode).Value;
    }

    [Fact]
    public async Task Returns_view_with_full_name_and_agent_code()
    {
        var agent = BuildAgent(agentCode: "MCB-001");
        _current.Setup(c => c.AgentId).Returns(agent.Id);
        _agents.Setup(r => r.GetByIdAsync(agent.Id, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        var handler = new GetCurrentAgentSummary(_agents.Object, _current.Object);
        var result = await handler.ExecuteAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FullName.Should().Be("Esteban Contreras");
        result.Value.AgentCode.Should().Be("MCB-001");
    }

    [Fact]
    public async Task Returns_view_with_null_agent_code_when_not_set()
    {
        var agent = BuildAgent(agentCode: null);
        _current.Setup(c => c.AgentId).Returns(agent.Id);
        _agents.Setup(r => r.GetByIdAsync(agent.Id, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        var handler = new GetCurrentAgentSummary(_agents.Object, _current.Object);
        var result = await handler.ExecuteAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AgentCode.Should().BeNull(
            because: "auto-provisioned agents have no commission code until admin assigns one");
    }

    [Fact]
    public async Task Fails_when_current_agent_is_not_in_repository()
    {
        var id = Guid.NewGuid();
        _current.Setup(c => c.AgentId).Returns(id);
        _agents.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Agent?)null);

        var handler = new GetCurrentAgentSummary(_agents.Object, _current.Object);
        var result = await handler.ExecuteAsync(CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }
}
