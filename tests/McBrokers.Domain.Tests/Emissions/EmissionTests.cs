using McBrokers.Domain.Emissions;

namespace McBrokers.Domain.Tests.Emissions;

public class EmissionTests
{
    private static readonly DateTime Now = new(2026, 5, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Start_creates_in_Pending_state()
    {
        var e = Emission.Start(Guid.NewGuid(), Guid.NewGuid(), Now).Value;

        e.Status.Should().Be(EmissionStatus.Pending);
        e.PolicyNumber.Should().BeNull();
        e.PdfBlobRef.Should().BeNull();
        e.IssuedAt.Should().BeNull();
    }

    [Fact]
    public void MarkIssued_transitions_to_Issued_and_sets_policy()
    {
        var e = Emission.Start(Guid.NewGuid(), Guid.NewGuid(), Now).Value;
        var issuedAt = Now.AddMinutes(2);

        e.MarkIssued("00000578041402", "blob://pdf/x.pdf", issuedAt);

        e.Status.Should().Be(EmissionStatus.Issued);
        e.PolicyNumber.Should().Be("00000578041402");
        e.PdfBlobRef.Should().Be("blob://pdf/x.pdf");
        e.IssuedAt.Should().Be(issuedAt);
        e.FailureReason.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_transitions_to_Failed_with_reason()
    {
        var e = Emission.Start(Guid.NewGuid(), Guid.NewGuid(), Now).Value;

        e.MarkFailed("La aseguradora rechazó la emisión.");

        e.Status.Should().Be(EmissionStatus.Failed);
        e.FailureReason.Should().Contain("rechazó");
    }

    [Fact]
    public void Start_rejects_non_utc_createdAt()
    {
        var local = DateTime.SpecifyKind(new DateTime(2026, 5, 11), DateTimeKind.Local);
        var result = Emission.Start(Guid.NewGuid(), Guid.NewGuid(), local);

        result.IsSuccess.Should().BeFalse();
    }
}
