using McBrokers.Application.Ports;
using McBrokers.Application.Quotations;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;

namespace McBrokers.Application.Tests.Quotations;

// Tests focalizados en las validaciones tempranas del use case. Las rutas
// que llegan al adapter (happy path + adapter Failure) se cubren en los
// tests del endpoint Api en Commit 3 con AdminApiFactory + adapter fake.
public class RequoteInsurerResultTests
{
    private readonly Mock<IQuotationRepository> _quotations = new(MockBehavior.Strict);
    private readonly Mock<IVehicleMasterRepository> _vehicles = new(MockBehavior.Strict);
    private readonly Mock<IVehicleInsurerMappingRepository> _mappings = new(MockBehavior.Strict);
    private readonly Mock<IInsurerRepository> _insurers = new(MockBehavior.Strict);
    private readonly Mock<IInsurerConfigRepository> _configs = new(MockBehavior.Strict);
    private readonly Mock<IInsurerPackageMappingRepository> _packageMappings = new(MockBehavior.Strict);
    private readonly Mock<IInsurerCredentialProvider> _credentials = new(MockBehavior.Strict);
    private readonly Mock<IAxaDxnConfigRepository> _axaDxnConfigs = new(MockBehavior.Strict);
    private readonly Mock<IBlobStore> _blob = new(MockBehavior.Strict);
    private readonly Mock<IClock> _clock = new(MockBehavior.Strict);
    private readonly Mock<IKnownInsurerErrorLookup> _errorLookup = new(MockBehavior.Strict);

    private static readonly DateTime NowUtc = DateTime.SpecifyKind(new(2026, 5, 13, 18, 0, 0), DateTimeKind.Utc);

    private RequoteInsurerResult BuildHandler() => new(
        _quotations.Object, _vehicles.Object, _mappings.Object,
        _insurers.Object, _configs.Object, _packageMappings.Object,
        _credentials.Object, _axaDxnConfigs.Object, _blob.Object,
        adapters: Array.Empty<IInsurerAdapter>(),
        clock: _clock.Object, errorLookup: _errorLookup.Object);

    private static Quotation BuildQuotation()
    {
        return Quotation.Create(
            Guid.NewGuid(), "corr", Guid.NewGuid(),
            PackageCode.Amplia, PaymentMode.Annual, ValuationType.Commercial,
            250_000m, "06700", "{}", NowUtc).Value;
    }

    private static RequoteInsurerCommand AnyCommand(Guid quotationId, Guid insurerId) =>
        new(quotationId, insurerId,
            OverrideVehicleMasterId: null,
            OverrideValuation: ValuationType.Agreed,
            OverrideDMPct: 10m,
            OverrideRTPct: 15m,
            OverrideGMO: 300_000m);

    [Fact]
    public async Task Fails_when_quotation_not_found()
    {
        var id = Guid.NewGuid();
        _quotations.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Quotation?)null);

        var result = await BuildHandler().ExecuteAsync(
            AnyCommand(id, Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Quotation");
    }

    [Fact]
    public async Task Fails_when_insurer_not_found()
    {
        var q = BuildQuotation();
        var insurerId = Guid.NewGuid();
        _quotations.Setup(r => r.GetByIdAsync(q.Id, It.IsAny<CancellationToken>())).ReturnsAsync(q);
        _insurers.Setup(r => r.GetByIdAsync(insurerId, It.IsAny<CancellationToken>())).ReturnsAsync((Insurer?)null);

        var result = await BuildHandler().ExecuteAsync(
            AnyCommand(q.Id, insurerId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Insurer");
    }

    [Fact]
    public async Task Fails_when_insurer_is_disabled()
    {
        var q = BuildQuotation();
        var insurer = Insurer.Create(InsurerCode.AxaCol, "AXA Colectividad", 5).Value;
        insurer.Disable();

        _quotations.Setup(r => r.GetByIdAsync(q.Id, It.IsAny<CancellationToken>())).ReturnsAsync(q);
        _insurers.Setup(r => r.GetByIdAsync(insurer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(insurer);

        var result = await BuildHandler().ExecuteAsync(
            AnyCommand(q.Id, insurer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("disabled");
    }

    [Fact]
    public async Task Fails_when_no_prior_current_result_for_that_insurer()
    {
        var q = BuildQuotation();
        var insurer = Insurer.Create(InsurerCode.Gnp, "GNP", 1).Value;

        _quotations.Setup(r => r.GetByIdAsync(q.Id, It.IsAny<CancellationToken>())).ReturnsAsync(q);
        _insurers.Setup(r => r.GetByIdAsync(insurer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(insurer);

        var result = await BuildHandler().ExecuteAsync(
            AnyCommand(q.Id, insurer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No prior result");
    }
}
