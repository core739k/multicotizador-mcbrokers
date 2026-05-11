using McBrokers.Application.Catalog;
using McBrokers.Application.Ports;
using McBrokers.Domain.Catalog;
using McBrokers.Domain.Catalog.Matching;

namespace McBrokers.Application.Tests.Catalog;

public class ImportInsurerCatalogTests
{
    private readonly Mock<IVehicleMasterRepository> _masters = new(MockBehavior.Strict);
    private readonly Mock<IVehicleInsurerMappingRepository> _mappings = new(MockBehavior.Strict);
    private readonly Mock<ICatalogImportBatchRepository> _batches = new(MockBehavior.Strict);
    private readonly Mock<IAuditWriter> _audit = new(MockBehavior.Strict);
    private readonly Mock<IClock> _clock = new(MockBehavior.Strict);
    private readonly Mock<ICurrentAgentProvider> _agent = new(MockBehavior.Strict);

    private static readonly DateTime Now = new(2026, 5, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid InsurerId = Guid.NewGuid();
    private static readonly Guid AgentId = Guid.NewGuid();

    public ImportInsurerCatalogTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _agent.SetupGet(a => a.AgentId).Returns(AgentId);
        _batches.Setup(b => b.AddAsync(It.IsAny<CatalogImportBatch>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _batches.Setup(b => b.UpdateAsync(It.IsAny<CatalogImportBatch>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _audit.Setup(a => a.WriteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private ImportInsurerCatalog Build() =>
        new(_masters.Object, _mappings.Object, _batches.Object, _audit.Object, _clock.Object, _agent.Object,
            new TextNormalizer());

    [Fact]
    public async Task Source_of_truth_seeds_VehicleMaster_with_approved_mapping()
    {
        _masters.Setup(r => r.AddAsync(It.IsAny<VehicleMaster>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mappings.Setup(r => r.FindByInsurerAndExternalClaveAsync(InsurerId, "21128", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleInsurerMapping?)null);
        _mappings.Setup(r => r.AddAsync(It.IsAny<VehicleInsurerMapping>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var rows = new[]
        {
            new CatalogImportRow(2025, "CHEVROLET", "AVEO", "LT", "21128", "SEDAN", "MANUAL"),
        };

        var result = await Build().ExecuteAsync(
            new ImportInsurerCatalogCommand(InsurerId, "Quálitas 2025", IsSourceOfTruth: true, rows),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Total.Should().Be(1);
        result.Value.AutoApproved.Should().Be(1);
        result.Value.PendingReview.Should().Be(0);

        _masters.Verify(r => r.AddAsync(
            It.Is<VehicleMaster>(v => v.Year == 2025 && v.Brand == "CHEVROLET" && v.Model == "AVEO" && v.Version == "LT"),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mappings.Verify(r => r.AddAsync(
            It.Is<VehicleInsurerMapping>(m =>
                m.InsurerId == InsurerId
                && m.ExternalClave == "21128"
                && m.ConfidenceScore == 100m
                && m.ReviewState == ReviewState.Approved),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Non_source_with_perfect_match_creates_approved_mapping()
    {
        var existing = VehicleMaster.Create(2025, "CHEVROLET", "AVEO", "LT", "SEDAN",
            VehicleTransmission.Manual, 4, 4).Value;

        _masters.Setup(r => r.FindByYearAndBrandAsync(2025, "CHEVROLET", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existing });
        _mappings.Setup(r => r.FindByInsurerAndExternalClaveAsync(InsurerId, "B0160133", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleInsurerMapping?)null);
        _mappings.Setup(r => r.AddAsync(It.IsAny<VehicleInsurerMapping>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var rows = new[]
        {
            new CatalogImportRow(2025, "CHEVROLET", "AVEO", "LT", "B0160133", null, null),
        };

        var result = await Build().ExecuteAsync(
            new ImportInsurerCatalogCommand(InsurerId, "AXA 2025", IsSourceOfTruth: false, rows),
            CancellationToken.None);

        result.Value.AutoApproved.Should().Be(1);
        _mappings.Verify(r => r.AddAsync(
            It.Is<VehicleInsurerMapping>(m =>
                m.VehicleMasterId == existing.Id
                && m.ConfidenceScore == 100m
                && m.ReviewState == ReviewState.Approved),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Non_source_with_partial_match_creates_pending_mapping()
    {
        var existing = VehicleMaster.Create(2025, "CHEVROLET", "AVEO", "LT PLUS PACK", "SEDAN",
            VehicleTransmission.Manual, 4, 4).Value;

        _masters.Setup(r => r.FindByYearAndBrandAsync(2025, "CHEVROLET", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existing });
        _mappings.Setup(r => r.FindByInsurerAndExternalClaveAsync(InsurerId, "21875", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleInsurerMapping?)null);
        _mappings.Setup(r => r.AddAsync(It.IsAny<VehicleInsurerMapping>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var rows = new[]
        {
            new CatalogImportRow(2025, "CHEVROLET", "AVEO", "LT", "21875", null, null),
        };

        var result = await Build().ExecuteAsync(
            new ImportInsurerCatalogCommand(InsurerId, "AXA 2025", IsSourceOfTruth: false, rows),
            CancellationToken.None);

        result.Value.PendingReview.Should().Be(1);
        _mappings.Verify(r => r.AddAsync(
            It.Is<VehicleInsurerMapping>(m =>
                m.ConfidenceScore < 95m && m.ReviewState == ReviewState.Pending),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Non_source_with_tied_top_match_creates_pending_mapping()
    {
        var candidateA = VehicleMaster.Create(2025, "CHEVROLET", "AVEO", "LT MANUAL", "SEDAN",
            VehicleTransmission.Manual, 4, 4).Value;
        var candidateB = VehicleMaster.Create(2025, "CHEVROLET", "AVEO", "LT AUTOMATICO", "SEDAN",
            VehicleTransmission.Automatic, 4, 4).Value;

        _masters.Setup(r => r.FindByYearAndBrandAsync(2025, "CHEVROLET", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { candidateA, candidateB });
        _mappings.Setup(r => r.FindByInsurerAndExternalClaveAsync(InsurerId, "X", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleInsurerMapping?)null);
        _mappings.Setup(r => r.AddAsync(It.IsAny<VehicleInsurerMapping>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var rows = new[]
        {
            // "AVEO LT" ties at score 2/3 against both candidates (2 tokens common, 1 different in each).
            new CatalogImportRow(2025, "CHEVROLET", "AVEO", "LT", "X", null, null),
        };

        var result = await Build().ExecuteAsync(
            new ImportInsurerCatalogCommand(InsurerId, "AXA 2025", IsSourceOfTruth: false, rows),
            CancellationToken.None);

        result.Value.PendingReview.Should().Be(1, "ties always go to manual review even when score is high");
        _mappings.Verify(r => r.AddAsync(
            It.Is<VehicleInsurerMapping>(m => m.ReviewState == ReviewState.Pending),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Non_source_with_no_brand_match_is_rejected()
    {
        _masters.Setup(r => r.FindByYearAndBrandAsync(2025, "UNKNOWN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<VehicleMaster>());
        _mappings.Setup(r => r.FindByInsurerAndExternalClaveAsync(InsurerId, "EXT", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleInsurerMapping?)null);

        var rows = new[]
        {
            new CatalogImportRow(2025, "Unknown", "Model", "Version", "EXT", null, null),
        };

        var result = await Build().ExecuteAsync(
            new ImportInsurerCatalogCommand(InsurerId, "X", IsSourceOfTruth: false, rows),
            CancellationToken.None);

        result.Value.Rejected.Should().Be(1);
        _mappings.Verify(r => r.AddAsync(It.IsAny<VehicleInsurerMapping>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Idempotency_skips_already_imported_external_clave()
    {
        var existingMapping = VehicleInsurerMapping.Create(
            Guid.NewGuid(), InsurerId, "21128", "B", "M", "V", 100m, Now).Value;

        _mappings.Setup(r => r.FindByInsurerAndExternalClaveAsync(InsurerId, "21128", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMapping);

        var rows = new[]
        {
            new CatalogImportRow(2025, "CHEVROLET", "AVEO", "LT", "21128", null, null),
        };

        var result = await Build().ExecuteAsync(
            new ImportInsurerCatalogCommand(InsurerId, "rerun", IsSourceOfTruth: true, rows),
            CancellationToken.None);

        result.Value.Total.Should().Be(1);
        _masters.Verify(r => r.AddAsync(It.IsAny<VehicleMaster>(), It.IsAny<CancellationToken>()), Times.Never);
        _mappings.Verify(r => r.AddAsync(It.IsAny<VehicleInsurerMapping>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
