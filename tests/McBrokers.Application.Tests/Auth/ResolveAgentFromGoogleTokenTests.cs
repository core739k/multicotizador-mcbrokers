using McBrokers.Application.Auth;
using McBrokers.Application.Ports;
using McBrokers.Domain.Agents;

namespace McBrokers.Application.Tests.Auth;

public class ResolveAgentFromGoogleTokenTests
{
    private readonly Mock<IAgentRepository> _repository = new(MockBehavior.Strict);

    [Fact]
    public async Task Returns_existing_active_agent_when_email_is_known()
    {
        var email = AgentEmail.Create("user@mcbrokers.com.mx").Value;
        var existing = Agent.Create(email, "Existing Agent", AgentRole.Agent).Value;

        _repository
            .Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = new ResolveAgentFromGoogleToken(_repository.Object);
        var identity = new GoogleIdentity(Email: "user@mcbrokers.com.mx", FullName: "Existing Agent");

        var result = await handler.ResolveAsync(identity, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(existing);
        _repository.Verify(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Provisions_new_agent_when_email_is_unknown_and_domain_is_valid()
    {
        var email = AgentEmail.Create("newcomer@mcbrokers.com.mx").Value;

        _repository
            .Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        _repository
            .Setup(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ResolveAgentFromGoogleToken(_repository.Object);
        var identity = new GoogleIdentity(Email: "newcomer@mcbrokers.com.mx", FullName: "Newcomer");

        var result = await handler.ResolveAsync(identity, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be(email);
        result.Value.FullName.Should().Be("Newcomer");
        result.Value.Role.Should().Be(AgentRole.Agent, "newcomers default to the Agent role");
        result.Value.IsActive.Should().BeTrue();

        _repository.Verify(r => r.AddAsync(It.Is<Agent>(a => a.Email == email), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Rejects_identity_with_invalid_domain()
    {
        var handler = new ResolveAgentFromGoogleToken(_repository.Object);
        var identity = new GoogleIdentity(Email: "intruder@gmail.com", FullName: "Intruder");

        var result = await handler.ResolveAsync(identity, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("mcbrokers.com.mx");
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Rejects_identity_with_empty_email()
    {
        var handler = new ResolveAgentFromGoogleToken(_repository.Object);
        var identity = new GoogleIdentity(Email: "   ", FullName: "Whatever");

        var result = await handler.ResolveAsync(identity, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Rejects_existing_inactive_agent()
    {
        var email = AgentEmail.Create("user@mcbrokers.com.mx").Value;
        var existing = Agent.Create(email, "Existing", AgentRole.Agent).Value;
        existing.Deactivate();

        _repository
            .Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = new ResolveAgentFromGoogleToken(_repository.Object);
        var identity = new GoogleIdentity(Email: "user@mcbrokers.com.mx", FullName: "Existing");

        var result = await handler.ResolveAsync(identity, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("inactive", because: "deactivated agents cannot sign in");
        _repository.Verify(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Uses_email_as_fallback_full_name_when_google_does_not_provide_one()
    {
        var email = AgentEmail.Create("nameless@mcbrokers.com.mx").Value;

        _repository
            .Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        _repository
            .Setup(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ResolveAgentFromGoogleToken(_repository.Object);
        var identity = new GoogleIdentity(Email: "nameless@mcbrokers.com.mx", FullName: "");

        var result = await handler.ResolveAsync(identity, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FullName.Should().Be("nameless@mcbrokers.com.mx");
    }
}
