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

    [Fact]
    public void CurrentResultFor_returns_null_when_no_result_for_insurer()
    {
        var q = BuildPending();
        q.CurrentResultFor(InsurerId).Should().BeNull();
    }

    [Fact]
    public void CurrentResultFor_returns_the_only_result_for_that_insurer()
    {
        var q = BuildPending();
        var r = QuotationInsurerResult.SucceededResult(
            q.Id, InsurerId, 100m, 80m, 16m, 4m, 100, "x", null, null, Now).Value;
        q.RecordResult(r);

        q.CurrentResultFor(InsurerId).Should().Be(r);
    }

    [Fact]
    public void SupersedeAndRecord_replaces_current_result_and_keeps_history()
    {
        var q = BuildPending();
        q.ExpectResultsFrom(1);
        var first = QuotationInsurerResult.SucceededResult(
            q.Id, InsurerId, 100m, 80m, 16m, 4m, 100, "v1", null, null, Now).Value;
        q.RecordResult(first);

        var overrides = new QuotationInsurerOverrides(
            null, ValuationType.Invoice, 10m, 15m, 300_000m);
        var second = QuotationInsurerResult.SucceededRequoteResult(
            q.Id, InsurerId, 200m, 160m, 32m, 8m, 90, "v2",
            null, null, Now, version: 2, overrides).Value;

        var outcome = q.SupersedeAndRecord(second);

        outcome.IsSuccess.Should().BeTrue();
        first.IsCurrent.Should().BeFalse();
        second.IsCurrent.Should().BeTrue();
        q.Results.Should().HaveCount(2, because: "the history is preserved");
        q.CurrentResultFor(InsurerId).Should().Be(second);
        q.Status.Should().Be(QuotationStatus.Completed,
            because: "the current count for the only expected insurer is still 1");
    }

    [Fact]
    public void SupersedeAndRecord_fails_when_no_prior_current()
    {
        var q = BuildPending();
        var overrides = new QuotationInsurerOverrides(null, ValuationType.Agreed, null, null, null);
        var requote = QuotationInsurerResult.SucceededRequoteResult(
            q.Id, InsurerId, 100m, 80m, 16m, 4m, 100, "x",
            null, null, Now, version: 2, overrides).Value;

        var outcome = q.SupersedeAndRecord(requote);

        outcome.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void SupersedeAndRecord_fails_when_version_is_not_prior_plus_one()
    {
        var q = BuildPending();
        var first = QuotationInsurerResult.SucceededResult(
            q.Id, InsurerId, 100m, 80m, 16m, 4m, 100, "v1", null, null, Now).Value;
        q.RecordResult(first);

        var overrides = new QuotationInsurerOverrides(null, ValuationType.Agreed, null, null, null);
        // Salta de 1 a 3 — debería fallar.
        var skipped = QuotationInsurerResult.SucceededRequoteResult(
            q.Id, InsurerId, 100m, 80m, 16m, 4m, 100, "v3",
            null, null, Now, version: 3, overrides).Value;

        var outcome = q.SupersedeAndRecord(skipped);

        outcome.IsSuccess.Should().BeFalse();
        first.IsCurrent.Should().BeTrue(because: "the failed supersede must not mutate state");
    }

    [Fact]
    public void Status_ignores_superseded_results_in_recompute()
    {
        var q = BuildPending();
        q.ExpectResultsFrom(2);
        var insurerA = Guid.NewGuid();
        var insurerB = Guid.NewGuid();

        // A: éxito v1
        q.RecordResult(QuotationInsurerResult.SucceededResult(
            q.Id, insurerA, 100m, 80m, 16m, 4m, 100, "a", null, null, Now).Value);
        // B: falla v1
        q.RecordResult(QuotationInsurerResult.FailedResult(
            q.Id, insurerB, QuotationInsurerStatus.InsurerDown, ErrorCategory.InsurerDown,
            "TIMEOUT", "Sin respuesta", 30000, null, null, Now).Value);
        q.Status.Should().Be(QuotationStatus.Completed,
            because: "any succeeded among 2 expected results => Completed");

        // Re-cotización de A con overrides → marca v1 superseded, agrega v2 también éxito.
        var overrides = new QuotationInsurerOverrides(null, ValuationType.Invoice, null, null, null);
        var v2 = QuotationInsurerResult.SucceededRequoteResult(
            q.Id, insurerA, 200m, 160m, 32m, 8m, 90, "a2",
            null, null, Now, version: 2, overrides).Value;
        q.SupersedeAndRecord(v2);

        // Status sigue Completed: 2 IsCurrent (A v2 + B v1), uno succeeded.
        q.Status.Should().Be(QuotationStatus.Completed);
    }
}
