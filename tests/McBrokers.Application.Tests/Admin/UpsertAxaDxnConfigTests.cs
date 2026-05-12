using McBrokers.Application.Admin;
using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Insurers.AxaDxn;

namespace McBrokers.Application.Tests.Admin;

public class UpsertAxaDxnConfigTests
{
    private readonly Mock<IInsurerRepository> _insurers = new(MockBehavior.Strict);
    private readonly Mock<IAxaDxnConfigRepository> _repo = new(MockBehavior.Strict);
    private readonly Mock<IAuditWriter> _audit = new(MockBehavior.Strict);

    private UpsertAxaDxnConfig Handler() => new(_insurers.Object, _repo.Object, _audit.Object);

    private static UpsertAxaDxnConfigCommand BuildCommand(Guid insurerId) => new(
        insurerId, "MCBROKERS", "secret", "RES", "PCK", 15, 20, 5);

    [Fact]
    public async Task Adds_new_config_when_none_exists()
    {
        var insurer = Insurer.Create(InsurerCode.AxaDxn, "AXA DXN", 5).Value;
        _insurers.Setup(r => r.GetByIdAsync(insurer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(insurer);
        _repo.Setup(r => r.GetByInsurerIdAsync(insurer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AxaDxnConfigWithBusinesses?)null);
        _repo.Setup(r => r.AddAsync(It.IsAny<AxaDxnConfig>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _audit.Setup(a => a.WriteAsync(
            "AxaDxnConfig.Create", "AxaDxnConfig", It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await Handler().ExecuteAsync(BuildCommand(insurer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.AddAsync(It.IsAny<AxaDxnConfig>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Updates_existing_config_when_one_already_present()
    {
        var insurer = Insurer.Create(InsurerCode.AxaDxn, "AXA DXN", 5).Value;
        var existingConfig = AxaDxnConfig.Create(insurer.Id, "old", "old-pwd", "OLD", "OLD-P", 5, 5, 1).Value;
        var snapshot = new AxaDxnConfigWithBusinesses(existingConfig, Array.Empty<AxaDxnBusiness>());

        _insurers.Setup(r => r.GetByIdAsync(insurer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(insurer);
        _repo.Setup(r => r.GetByInsurerIdAsync(insurer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        _repo.Setup(r => r.UpdateAsync(existingConfig, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _audit.Setup(a => a.WriteAsync(
            "AxaDxnConfig.Update", "AxaDxnConfig", existingConfig.Id.ToString(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await Handler().ExecuteAsync(BuildCommand(insurer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        existingConfig.Usuario.Should().Be("MCBROKERS");
        existingConfig.Tarifa.Should().Be("RES");
        existingConfig.Descuento.Should().Be(15);
    }

    [Fact]
    public async Task Fails_when_insurer_does_not_exist()
    {
        var unknown = Guid.NewGuid();
        _insurers.Setup(r => r.GetByIdAsync(unknown, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Insurer?)null);

        var result = await Handler().ExecuteAsync(BuildCommand(unknown), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Insurer");
    }

    [Fact]
    public async Task Fails_when_validation_rejects_command()
    {
        var insurer = Insurer.Create(InsurerCode.AxaDxn, "AXA DXN", 5).Value;
        _insurers.Setup(r => r.GetByIdAsync(insurer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(insurer);
        _repo.Setup(r => r.GetByInsurerIdAsync(insurer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AxaDxnConfigWithBusinesses?)null);

        // mes 0 is invalid (must be 1..12)
        var invalidCommand = new UpsertAxaDxnConfigCommand(insurer.Id, "u", "p", "RES", "PCK", 0, 0, 0);
        var result = await Handler().ExecuteAsync(invalidCommand, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _repo.Verify(r => r.AddAsync(It.IsAny<AxaDxnConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
