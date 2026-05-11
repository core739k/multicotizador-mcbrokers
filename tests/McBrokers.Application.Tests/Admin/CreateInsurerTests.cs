using McBrokers.Application.Admin;
using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers;

namespace McBrokers.Application.Tests.Admin;

public class CreateInsurerTests
{
    private readonly Mock<IInsurerRepository> _insurers = new(MockBehavior.Strict);
    private readonly Mock<IAuditWriter> _audit = new(MockBehavior.Strict);

    private CreateInsurer Handler() => new(_insurers.Object, _audit.Object);

    [Fact]
    public async Task Creates_insurer_and_writes_audit_when_code_is_new()
    {
        _insurers
            .Setup(r => r.GetByCodeAsync(InsurerCode.Gnp, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Insurer?)null);
        _insurers
            .Setup(r => r.AddAsync(It.IsAny<Insurer>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _audit
            .Setup(a => a.WriteAsync(
                "Insurer.Create", "Insurer", It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await Handler().ExecuteAsync(
            new CreateInsurerCommand(InsurerCode.Gnp, "Grupo Nacional Provincial", DisplayOrder: 1),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);

        _insurers.Verify(
            r => r.AddAsync(It.Is<Insurer>(i => i.Code == InsurerCode.Gnp && i.Name == "Grupo Nacional Provincial"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _audit.Verify(
            a => a.WriteAsync("Insurer.Create", "Insurer", It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Rejects_duplicate_code()
    {
        var existing = Insurer.Create(InsurerCode.Gnp, "Existing", 1).Value;
        _insurers
            .Setup(r => r.GetByCodeAsync(InsurerCode.Gnp, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await Handler().ExecuteAsync(
            new CreateInsurerCommand(InsurerCode.Gnp, "Another Name", 2),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already exists");
        _insurers.Verify(r => r.AddAsync(It.IsAny<Insurer>(), It.IsAny<CancellationToken>()), Times.Never);
        _audit.Verify(a => a.WriteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Propagates_domain_validation_error()
    {
        _insurers
            .Setup(r => r.GetByCodeAsync(InsurerCode.Gnp, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Insurer?)null);

        var result = await Handler().ExecuteAsync(
            new CreateInsurerCommand(InsurerCode.Gnp, Name: "", DisplayOrder: 1),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _insurers.Verify(r => r.AddAsync(It.IsAny<Insurer>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
