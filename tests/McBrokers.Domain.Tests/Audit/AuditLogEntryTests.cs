using McBrokers.Domain.Audit;

namespace McBrokers.Domain.Tests.Audit;

public class AuditLogEntryTests
{
    [Fact]
    public void Create_with_valid_inputs_succeeds()
    {
        var when = new DateTime(2026, 5, 11, 18, 30, 0, DateTimeKind.Utc);

        var result = AuditLogEntry.Create(
            agentId: Guid.NewGuid(),
            action: "CreateInsurer",
            entityType: nameof(McBrokers.Domain.Insurers.Insurer),
            entityId: "GNP",
            correlationId: "corr-abc-123",
            payloadJson: """{"code":"GNP","name":"GNP"}""",
            createdAt: when);

        result.IsSuccess.Should().BeTrue();
        var entry = result.Value;
        entry.Id.Should().NotBe(Guid.Empty);
        entry.AgentId.Should().NotBeNull();
        entry.Action.Should().Be("CreateInsurer");
        entry.EntityType.Should().Be("Insurer");
        entry.EntityId.Should().Be("GNP");
        entry.CorrelationId.Should().Be("corr-abc-123");
        entry.PayloadJson.Should().Contain("GNP");
        entry.CreatedAt.Should().Be(when);
    }

    [Fact]
    public void Create_allows_null_agent_for_system_actions()
    {
        var result = AuditLogEntry.Create(
            agentId: null,
            action: "SystemBoot",
            entityType: "System",
            entityId: "n/a",
            correlationId: null,
            payloadJson: "{}",
            createdAt: DateTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        result.Value.AgentId.Should().BeNull();
    }

    [Fact]
    public void Create_rejects_non_utc_timestamps()
    {
        var localTime = DateTime.SpecifyKind(new DateTime(2026, 5, 11, 12, 0, 0), DateTimeKind.Local);

        var result = AuditLogEntry.Create(
            agentId: Guid.NewGuid(),
            action: "CreateInsurer",
            entityType: "Insurer",
            entityId: "GNP",
            correlationId: null,
            payloadJson: "{}",
            createdAt: localTime);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("UTC");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_action(string? action)
    {
        var result = AuditLogEntry.Create(
            agentId: Guid.NewGuid(),
            action: action!,
            entityType: "Insurer",
            entityId: "GNP",
            correlationId: null,
            payloadJson: "{}",
            createdAt: DateTime.UtcNow);

        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_rejects_empty_entity_type(string? entityType)
    {
        var result = AuditLogEntry.Create(
            agentId: Guid.NewGuid(),
            action: "X",
            entityType: entityType!,
            entityId: "Y",
            correlationId: null,
            payloadJson: "{}",
            createdAt: DateTime.UtcNow);

        result.IsSuccess.Should().BeFalse();
    }
}
