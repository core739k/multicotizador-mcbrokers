using McBrokers.Application.Catalog;
using McBrokers.Application.Ports;
using McBrokers.Domain.Catalog;

namespace McBrokers.Application.Tests.Catalog;

public class DecideMappingTests
{
    private readonly Mock<IVehicleInsurerMappingRepository> _mappings = new(MockBehavior.Strict);
    private readonly Mock<IAuditWriter> _audit = new(MockBehavior.Strict);
    private readonly Mock<IClock> _clock = new(MockBehavior.Strict);
    private readonly Mock<ICurrentAgentProvider> _agent = new(MockBehavior.Strict);

    private static readonly DateTime Now = new(2026, 5, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid AdminId = Guid.NewGuid();

    public DecideMappingTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _agent.SetupGet(a => a.AgentId).Returns(AdminId);
        _audit.Setup(a => a.WriteAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private DecideMapping Build() =>
        new(_mappings.Object, _audit.Object, _clock.Object, _agent.Object);

    private static VehicleInsurerMapping PendingMapping() =>
        VehicleInsurerMapping.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            "EXT-123", "B", "M", "V", 80m, Now).Value;

    [Fact]
    public async Task Approve_pending_mapping_marks_approved_and_audits()
    {
        var mapping = PendingMapping();
        _mappings.Setup(r => r.GetByIdAsync(mapping.Id, It.IsAny<CancellationToken>())).ReturnsAsync(mapping);
        _mappings.Setup(r => r.UpdateAsync(mapping, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await Build().ExecuteAsync(
            new DecideMappingCommand(mapping.Id, Decision.Approve),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        mapping.ReviewState.Should().Be(ReviewState.Approved);
        mapping.ReviewedByAgentId.Should().Be(AdminId);
        mapping.ReviewedAt.Should().Be(Now);

        _audit.Verify(a => a.WriteAsync(
            "CatalogMapping.Approve", "VehicleInsurerMapping", mapping.Id.ToString(),
            It.IsAny<object?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Reject_pending_mapping_marks_rejected_and_audits()
    {
        var mapping = PendingMapping();
        _mappings.Setup(r => r.GetByIdAsync(mapping.Id, It.IsAny<CancellationToken>())).ReturnsAsync(mapping);
        _mappings.Setup(r => r.UpdateAsync(mapping, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await Build().ExecuteAsync(
            new DecideMappingCommand(mapping.Id, Decision.Reject),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        mapping.ReviewState.Should().Be(ReviewState.Rejected);
        _audit.Verify(a => a.WriteAsync(
            "CatalogMapping.Reject", "VehicleInsurerMapping", mapping.Id.ToString(),
            It.IsAny<object?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Fails_when_mapping_not_found()
    {
        var id = Guid.NewGuid();
        _mappings.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((VehicleInsurerMapping?)null);

        var result = await Build().ExecuteAsync(
            new DecideMappingCommand(id, Decision.Approve),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
