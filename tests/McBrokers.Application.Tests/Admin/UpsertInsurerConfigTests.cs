using McBrokers.Application.Admin;
using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers;

namespace McBrokers.Application.Tests.Admin;

public class UpsertInsurerConfigTests
{
    private readonly Mock<IInsurerRepository> _insurers = new(MockBehavior.Strict);
    private readonly Mock<IInsurerConfigRepository> _configs = new(MockBehavior.Strict);
    private readonly Mock<IAuditWriter> _audit = new(MockBehavior.Strict);

    private UpsertInsurerConfig Handler() => new(_insurers.Object, _configs.Object, _audit.Object);

    private static UpsertInsurerConfigCommand BuildCommand(Guid insurerId) => new(
        InsurerId: insurerId,
        EndpointUrl: "https://insurer.example.com/ws",
        BusinessNumber: "12345",
        AgentCode: "AGT001",
        KeyVaultSecretName: "insurers--gnp--credentials",
        TimeoutSeconds: 30,
        MaxRetries: 3);

    [Fact]
    public async Task Adds_new_config_when_none_exists_for_insurer()
    {
        var insurer = Insurer.Create(InsurerCode.Gnp, "GNP", 1).Value;
        _insurers
            .Setup(r => r.GetByIdAsync(insurer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(insurer);
        _configs
            .Setup(r => r.GetAsync(insurer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InsurerConfig?)null);
        _configs
            .Setup(r => r.AddAsync(It.IsAny<InsurerConfig>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _audit
            .Setup(a => a.WriteAsync(
                "InsurerConfig.Create", "InsurerConfig", It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await Handler().ExecuteAsync(BuildCommand(insurer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _configs.Verify(r => r.AddAsync(It.IsAny<InsurerConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        _configs.Verify(r => r.UpdateAsync(It.IsAny<InsurerConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Updates_existing_config_when_one_already_present()
    {
        var insurer = Insurer.Create(InsurerCode.Gnp, "GNP", 1).Value;
        var existing = InsurerConfig.Create(
            insurer.Id,
            "https://old.example.com", "old", "old", "kv-old", 10, 1).Value;

        _insurers
            .Setup(r => r.GetByIdAsync(insurer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(insurer);
        _configs
            .Setup(r => r.GetAsync(insurer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _configs
            .Setup(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _audit
            .Setup(a => a.WriteAsync(
                "InsurerConfig.Update", "InsurerConfig", existing.Id.ToString(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await Handler().ExecuteAsync(BuildCommand(insurer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        existing.EndpointUrl.Should().Be("https://insurer.example.com/ws");
        existing.TimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public async Task Fails_when_insurer_does_not_exist()
    {
        var unknown = Guid.NewGuid();
        _insurers
            .Setup(r => r.GetByIdAsync(unknown, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Insurer?)null);

        var result = await Handler().ExecuteAsync(BuildCommand(unknown), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Insurer");
    }
}
