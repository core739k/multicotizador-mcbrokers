using McBrokers.Domain.Quotations;

namespace McBrokers.Domain.Tests.Quotations;

public class QuotationTests
{
    private static readonly Guid AgentId = Guid.NewGuid();
    private static readonly Guid VehicleMasterId = Guid.NewGuid();
    private static readonly Guid InsurerId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 5, 11, 12, 0, 0, DateTimeKind.Utc);

    private static Quotation BuildPending()
    {
        return Quotation.Create(
            agentId: AgentId,
            correlationId: "corr-123",
            vehicleMasterId: VehicleMasterId,
            package: PackageCode.Amplia,
            paymentMode: PaymentMode.Annual,
            valuationType: ValuationType.Commercial,
            sumInsured: 250000m,
            postalCode: "06700",
            customerSnapshotJson: """{"name":"Juan Pérez"}""",
            createdAt: Now).Value;
    }

    [Fact]
    public void Create_with_valid_inputs_succeeds_in_pending_state()
    {
        var quotation = BuildPending();

        quotation.Id.Should().NotBe(Guid.Empty);
        quotation.CorrelationId.Should().Be("corr-123");
        quotation.Status.Should().Be(QuotationStatus.Pending);
        quotation.AgentId.Should().Be(AgentId);
        quotation.VehicleMasterId.Should().Be(VehicleMasterId);
        quotation.Package.Should().Be(PackageCode.Amplia);
        quotation.SumInsured.Should().Be(250000m);
        quotation.PostalCode.Should().Be("06700");
        quotation.Results.Should().BeEmpty();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Create_rejects_non_positive_sum_insured(decimal sum)
    {
        var result = Quotation.Create(AgentId, "c", VehicleMasterId,
            PackageCode.Amplia, PaymentMode.Annual, ValuationType.Commercial,
            sum, "06700", "{}", Now);

        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234")]    // 4 dígitos
    [InlineData("123456")]  // 6 dígitos
    [InlineData("abcde")]   // no numérico
    public void Create_rejects_invalid_postal_code(string? cp)
    {
        var result = Quotation.Create(AgentId, "c", VehicleMasterId,
            PackageCode.Amplia, PaymentMode.Annual, ValuationType.Commercial,
            250000m, cp!, "{}", Now);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void RecordResult_appends_to_results()
    {
        var q = BuildPending();
        var r = QuotationInsurerResult.SucceededResult(
            quotationId: q.Id, insurerId: InsurerId,
            premiumTotal: 11638.92m, premiumNet: 9493.54m, tax: 1605.38m, fees: 540m,
            latencyMs: 1234, externalQuoteRef: "CIANNE231023021110",
            requestBlobRef: "blob://reqs/c1.xml", responseBlobRef: "blob://res/c1.xml",
            createdAt: Now).Value;

        q.RecordResult(r);

        q.Results.Should().HaveCount(1);
        q.Results[0].Should().Be(r);
    }

    [Fact]
    public void Status_becomes_Partial_when_one_of_two_expected_insurers_returns()
    {
        var q = BuildPending();
        q.ExpectResultsFrom(2);
        var r = QuotationInsurerResult.SucceededResult(
            q.Id, InsurerId, 100m, 80m, 16m, 4m, 100, "x", "r", "p", Now).Value;

        q.RecordResult(r);

        q.Status.Should().Be(QuotationStatus.Partial);
    }

    [Fact]
    public void Status_becomes_Completed_when_all_expected_results_returned()
    {
        var q = BuildPending();
        q.ExpectResultsFrom(2);
        q.RecordResult(QuotationInsurerResult.SucceededResult(
            q.Id, InsurerId, 100m, 80m, 16m, 4m, 100, "x", "r", "p", Now).Value);
        q.RecordResult(QuotationInsurerResult.SucceededResult(
            q.Id, Guid.NewGuid(), 200m, 180m, 16m, 4m, 100, "y", "r", "p", Now).Value);

        q.Status.Should().Be(QuotationStatus.Completed);
    }

    [Fact]
    public void Status_becomes_Failed_when_all_results_failed()
    {
        var q = BuildPending();
        q.ExpectResultsFrom(1);
        q.RecordResult(QuotationInsurerResult.FailedResult(
            q.Id, InsurerId, QuotationInsurerStatus.InsurerDown, ErrorCategory.InsurerDown,
            errorCode: "TIMEOUT", errorMessageHuman: "GNP no responde",
            latencyMs: 30000, requestBlobRef: null, responseBlobRef: null, createdAt: Now).Value);

        q.Status.Should().Be(QuotationStatus.Failed);
    }
}
