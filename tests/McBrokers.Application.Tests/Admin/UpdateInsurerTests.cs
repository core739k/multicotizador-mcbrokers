using McBrokers.Application.Admin;
using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers;

namespace McBrokers.Application.Tests.Admin;

public class UpdateInsurerTests
{
    private readonly Mock<IInsurerRepository> _insurers = new(MockBehavior.Strict);
    private readonly Mock<IAuditWriter> _audit = new(MockBehavior.Strict);

    private UpdateInsurer Handler() => new(_insurers.Object, _audit.Object);

    [Fact]
    public async Task Renames_reorders_and_toggles_in_one_call()
    {
        var existing = Insurer.Create(InsurerCode.Gnp, "Old", 5).Value;
        _insurers
            .Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _insurers
            .Setup(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _audit
            .Setup(a => a.WriteAsync(
                "Insurer.Update", "Insurer", existing.Id.ToString(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd = new UpdateInsurerCommand(
            Id: existing.Id,
            Name: "GNP",
            DisplayOrder: 1,
            IsEnabled: false,
            LogoUrl: "https://cdn.example.com/gnp.png");

        var result = await Handler().ExecuteAsync(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        existing.Name.Should().Be("GNP");
        existing.DisplayOrder.Should().Be(1);
        existing.IsEnabled.Should().BeFalse();
        existing.LogoUrl.Should().Be("https://cdn.example.com/gnp.png");
    }

    [Fact]
    public async Task Fails_when_insurer_not_found()
    {
        var id = Guid.NewGuid();
        _insurers
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Insurer?)null);

        var result = await Handler().ExecuteAsync(
            new UpdateInsurerCommand(id, "X", 0, true, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }
}
