using McBrokers.Domain.Quotations;

namespace McBrokers.Domain.Tests.Quotations;

public class QuotationInsurerResultTests
{
    private static readonly Guid QuotationId = Guid.NewGuid();
    private static readonly Guid InsurerId = Guid.NewGuid();
    private static readonly DateTime NowUtc = DateTime.SpecifyKind(new(2026, 5, 13, 16, 0, 0), DateTimeKind.Utc);

    [Fact]
    public void SucceededResult_defaults_version_to_1_and_is_current_true()
    {
        var r = QuotationInsurerResult.SucceededResult(
            QuotationId, InsurerId,
            premiumTotal: 100m, premiumNet: 80m, tax: 16m, fees: 4m,
            latencyMs: 100, externalQuoteRef: "x",
            requestBlobRef: "r", responseBlobRef: "p", createdAt: NowUtc).Value;

        r.Version.Should().Be(1);
        r.IsCurrent.Should().BeTrue();
        r.Overrides.Should().BeNull();
    }

    [Fact]
    public void FailedResult_defaults_version_to_1_and_is_current_true()
    {
        var r = QuotationInsurerResult.FailedResult(
            QuotationId, InsurerId,
            QuotationInsurerStatus.InsurerDown, ErrorCategory.InsurerDown,
            "TIMEOUT", "Sin respuesta",
            latencyMs: 30000, requestBlobRef: null, responseBlobRef: null, createdAt: NowUtc).Value;

        r.Version.Should().Be(1);
        r.IsCurrent.Should().BeTrue();
        r.Overrides.Should().BeNull();
    }

    [Fact]
    public void SucceededRequoteResult_records_version_and_overrides()
    {
        var overrides = new QuotationInsurerOverrides(
            VehicleMasterId: null,
            Valuation: ValuationType.Invoice,
            MaterialDamagesDeductiblePct: 10m,
            RobberyDeductiblePct: 15m,
            MedicalExpensesSumInsured: 300_000m);

        var r = QuotationInsurerResult.SucceededRequoteResult(
            QuotationId, InsurerId,
            premiumTotal: 200m, premiumNet: 160m, tax: 32m, fees: 8m,
            latencyMs: 800, externalQuoteRef: "y",
            requestBlobRef: null, responseBlobRef: null, createdAt: NowUtc,
            version: 2, overrides: overrides).Value;

        r.Version.Should().Be(2);
        r.IsCurrent.Should().BeTrue();
        r.Overrides.Should().Be(overrides);
    }

    [Fact]
    public void SucceededRequoteResult_rejects_version_below_2()
    {
        var overrides = new QuotationInsurerOverrides(null, ValuationType.Agreed, null, null, null);

        var result = QuotationInsurerResult.SucceededRequoteResult(
            QuotationId, InsurerId,
            100m, 80m, 16m, 4m, 100, "x", null, null, NowUtc,
            version: 1, overrides: overrides);

        result.IsSuccess.Should().BeFalse(
            because: "version 1 is reserved for the initial result; requotes start at 2");
    }

    [Fact]
    public void Supersede_marks_result_as_not_current_idempotently()
    {
        var r = QuotationInsurerResult.SucceededResult(
            QuotationId, InsurerId, 100m, 80m, 16m, 4m, 100, "x", null, null, NowUtc).Value;

        r.Supersede();
        r.IsCurrent.Should().BeFalse();

        r.Supersede();
        r.IsCurrent.Should().BeFalse(because: "calling Supersede on an already-superseded result is a no-op");
    }
}
