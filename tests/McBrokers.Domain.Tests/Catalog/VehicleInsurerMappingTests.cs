using McBrokers.Domain.Catalog;

namespace McBrokers.Domain.Tests.Catalog;

public class VehicleInsurerMappingTests
{
    private static readonly Guid MasterId = Guid.NewGuid();
    private static readonly Guid InsurerId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 5, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_autoapproves_when_score_meets_threshold()
    {
        var result = VehicleInsurerMapping.Create(
            MasterId, InsurerId,
            externalClave: "21128",
            insurerBrandRaw: "CHEVROLET",
            insurerModelRaw: "AVEO",
            insurerVersionRaw: "AVEO LT",
            confidenceScore: 97m,
            createdAt: Now);

        result.IsSuccess.Should().BeTrue();
        var mapping = result.Value;
        mapping.VehicleMasterId.Should().Be(MasterId);
        mapping.InsurerId.Should().Be(InsurerId);
        mapping.ExternalClave.Should().Be("21128");
        mapping.ConfidenceScore.Should().Be(97m);
        mapping.ReviewState.Should().Be(ReviewState.Approved,
            because: "score >= 95 is the auto-approval threshold");
    }

    [Theory]
    [InlineData(94.99)]
    [InlineData(80)]
    [InlineData(50)]
    public void Create_marks_pending_when_score_below_threshold(decimal score)
    {
        var mapping = VehicleInsurerMapping.Create(
            MasterId, InsurerId, "X", "B", "M", "V", score, Now).Value;

        mapping.ReviewState.Should().Be(ReviewState.Pending);
    }

    [Fact]
    public void Create_clamps_score_to_0_100()
    {
        VehicleInsurerMapping.Create(MasterId, InsurerId, "X", "B", "M", "V", -10m, Now).IsSuccess.Should().BeFalse();
        VehicleInsurerMapping.Create(MasterId, InsurerId, "X", "B", "M", "V", 101m, Now).IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_external_clave(string? clave)
    {
        var result = VehicleInsurerMapping.Create(MasterId, InsurerId, clave!, "B", "M", "V", 100m, Now);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Approve_changes_state_and_records_reviewer()
    {
        var mapping = VehicleInsurerMapping.Create(MasterId, InsurerId, "X", "B", "M", "V", 80m, Now).Value;

        mapping.Approve(by: AdminId, at: Now.AddMinutes(5));

        mapping.ReviewState.Should().Be(ReviewState.Approved);
        mapping.ReviewedByAgentId.Should().Be(AdminId);
        mapping.ReviewedAt.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public void Reject_changes_state_and_records_reviewer()
    {
        var mapping = VehicleInsurerMapping.Create(MasterId, InsurerId, "X", "B", "M", "V", 80m, Now).Value;

        mapping.Reject(by: AdminId, at: Now.AddMinutes(5));

        mapping.ReviewState.Should().Be(ReviewState.Rejected);
        mapping.ReviewedByAgentId.Should().Be(AdminId);
        mapping.ReviewedAt.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public void Approve_rejects_non_utc_timestamp()
    {
        var mapping = VehicleInsurerMapping.Create(MasterId, InsurerId, "X", "B", "M", "V", 80m, Now).Value;
        var local = DateTime.SpecifyKind(new DateTime(2026, 5, 11), DateTimeKind.Local);

        var action = () => mapping.Approve(AdminId, local);

        action.Should().Throw<ArgumentException>();
    }
}
