using McBrokers.Application.Ports;
using McBrokers.Application.Quotations;
using McBrokers.Domain.Catalog;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Quotations;

namespace McBrokers.Application.Tests.Quotations;

public class GetQuotationStatusTests
{
    private readonly Mock<IQuotationRepository> _quotations = new(MockBehavior.Strict);
    private readonly Mock<IVehicleMasterRepository> _vehicles = new(MockBehavior.Strict);
    private readonly Mock<IInsurerRepository> _insurers = new(MockBehavior.Strict);

    public GetQuotationStatusTests()
    {
        // Default: la mayoría de los tests no se enfocan en insurers — los tests
        // específicos sobreescriben este setup.
        _insurers.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Array.Empty<Insurer>());
    }

    private static readonly Guid AgentId = Guid.NewGuid();
    private static readonly DateTime NowUtc = DateTime.SpecifyKind(new DateTime(2026, 5, 13, 12, 0, 0), DateTimeKind.Utc);

    private static Quotation BuildQuotation(Guid vehicleMasterId, string snapshotJson)
    {
        return Quotation.Create(
            AgentId, "corr-001", vehicleMasterId,
            PackageCode.Amplia, PaymentMode.Annual, ValuationType.Commercial,
            sumInsured: 250_000m, postalCode: "06700",
            customerSnapshotJson: snapshotJson, createdAt: NowUtc).Value;
    }

    private static VehicleMaster BuildVehicle(Guid id)
    {
        var v = VehicleMaster.Create(
            year: 2024, brand: "VW", model: "JETTA", version: "TRENDLINE",
            bodyType: "Sedán", transmission: VehicleTransmission.Automatic,
            doors: 4, cylinders: 4).Value;
        var idField = typeof(VehicleMaster).GetField("<Id>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        idField!.SetValue(v, id);
        return v;
    }

    [Fact]
    public async Task Returns_null_when_quotation_not_found()
    {
        var id = Guid.NewGuid();
        _quotations.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Quotation?)null);

        var handler = new GetQuotationStatus(_quotations.Object, _vehicles.Object, _insurers.Object);
        var view = await handler.ExecuteAsync(id, CancellationToken.None);

        view.Should().BeNull();
    }

    [Fact]
    public async Task View_includes_vehicle_info_when_master_exists()
    {
        var vehicleId = Guid.NewGuid();
        var q = BuildQuotation(vehicleId, "{}");
        _quotations.Setup(r => r.GetByIdAsync(q.Id, It.IsAny<CancellationToken>())).ReturnsAsync(q);
        _vehicles.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(BuildVehicle(vehicleId));

        var handler = new GetQuotationStatus(_quotations.Object, _vehicles.Object, _insurers.Object);
        var view = await handler.ExecuteAsync(q.Id, CancellationToken.None);

        view.Should().NotBeNull();
        view!.Vehicle.Should().NotBeNull();
        view.Vehicle!.Year.Should().Be(2024);
        view.Vehicle.Brand.Should().Be("VW");
        view.Vehicle.Model.Should().Be("JETTA");
        view.Vehicle.Version.Should().Be("TRENDLINE");
    }

    [Fact]
    public async Task View_has_null_vehicle_when_master_missing()
    {
        var vehicleId = Guid.NewGuid();
        var q = BuildQuotation(vehicleId, "{}");
        _quotations.Setup(r => r.GetByIdAsync(q.Id, It.IsAny<CancellationToken>())).ReturnsAsync(q);
        _vehicles.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>())).ReturnsAsync((VehicleMaster?)null);

        var handler = new GetQuotationStatus(_quotations.Object, _vehicles.Object, _insurers.Object);
        var view = await handler.ExecuteAsync(q.Id, CancellationToken.None);

        view!.Vehicle.Should().BeNull(
            because: "the screen should degrade gracefully if the vehicle was deleted from the catalog");
    }

    [Fact]
    public async Task View_exposes_sum_insured()
    {
        var vehicleId = Guid.NewGuid();
        var q = BuildQuotation(vehicleId, "{}");
        _quotations.Setup(r => r.GetByIdAsync(q.Id, It.IsAny<CancellationToken>())).ReturnsAsync(q);
        _vehicles.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(BuildVehicle(vehicleId));

        var handler = new GetQuotationStatus(_quotations.Object, _vehicles.Object, _insurers.Object);
        var view = await handler.ExecuteAsync(q.Id, CancellationToken.None);

        view!.SumInsured.Should().Be(250_000m);
    }

    [Fact]
    public async Task Deducibles_parsed_from_snapshot()
    {
        var vehicleId = Guid.NewGuid();
        var snapshot = """
            {
              "Deductibles": {
                "MaterialDamagesDeductiblePct": 5,
                "RobberyDeductiblePct": 10,
                "MedicalExpensesSumInsured": 200000,
                "CivilLiabilitySumInsured": 3000000
              }
            }
            """;
        var q = BuildQuotation(vehicleId, snapshot);
        _quotations.Setup(r => r.GetByIdAsync(q.Id, It.IsAny<CancellationToken>())).ReturnsAsync(q);
        _vehicles.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(BuildVehicle(vehicleId));

        var handler = new GetQuotationStatus(_quotations.Object, _vehicles.Object, _insurers.Object);
        var view = await handler.ExecuteAsync(q.Id, CancellationToken.None);

        view!.Deducibles.Should().NotBeNull();
        view.Deducibles!.MaterialDamagesDeductiblePct.Should().Be(5m);
        view.Deducibles.RobberyDeductiblePct.Should().Be(10m);
        view.Deducibles.MedicalExpensesSumInsured.Should().Be(200_000m);
        view.Deducibles.CivilLiabilitySumInsured.Should().Be(3_000_000m);
    }

    [Fact]
    public async Task Deducibles_is_null_when_snapshot_has_no_deductibles_section()
    {
        var vehicleId = Guid.NewGuid();
        var q = BuildQuotation(vehicleId, """{ "Contractor": { } }""");
        _quotations.Setup(r => r.GetByIdAsync(q.Id, It.IsAny<CancellationToken>())).ReturnsAsync(q);
        _vehicles.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(BuildVehicle(vehicleId));

        var handler = new GetQuotationStatus(_quotations.Object, _vehicles.Object, _insurers.Object);
        var view = await handler.ExecuteAsync(q.Id, CancellationToken.None);

        view!.Deducibles.Should().BeNull();
    }

    [Fact]
    public async Task Deducibles_is_null_when_snapshot_is_malformed_json()
    {
        var vehicleId = Guid.NewGuid();
        var q = BuildQuotation(vehicleId, "not-json-at-all");
        _quotations.Setup(r => r.GetByIdAsync(q.Id, It.IsAny<CancellationToken>())).ReturnsAsync(q);
        _vehicles.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(BuildVehicle(vehicleId));

        var handler = new GetQuotationStatus(_quotations.Object, _vehicles.Object, _insurers.Object);
        var view = await handler.ExecuteAsync(q.Id, CancellationToken.None);

        view!.Deducibles.Should().BeNull(because: "the view should not crash on bad snapshots");
    }

    [Fact]
    public async Task Result_view_enriches_each_result_with_insurer_code_name_and_logo()
    {
        var vehicleId = Guid.NewGuid();
        var insurerId = Guid.NewGuid();
        var q = BuildQuotation(vehicleId, "{}");

        // Inyectamos un QuotationInsurerResult con InsurerId conocido.
        var result = QuotationInsurerResult.SucceededResult(
            q.Id, insurerId,
            premiumTotal: 11000m, premiumNet: 9500m, tax: 1500m, fees: 0m,
            latencyMs: 1234, externalQuoteRef: "GNP-001",
            requestBlobRef: null, responseBlobRef: null, createdAt: NowUtc).Value;
        q.RecordResult(result);

        var gnp = Insurer.Create(InsurerCode.Gnp, "Grupo Nacional Provincial", displayOrder: 1).Value;
        ForceId(gnp, insurerId);

        _quotations.Setup(r => r.GetByIdAsync(q.Id, It.IsAny<CancellationToken>())).ReturnsAsync(q);
        _vehicles.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildVehicle(vehicleId));
        _insurers.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { gnp });

        var handler = new GetQuotationStatus(_quotations.Object, _vehicles.Object, _insurers.Object);
        var view = await handler.ExecuteAsync(q.Id, CancellationToken.None);

        view!.Results.Should().HaveCount(1);
        var row = view.Results[0];
        row.InsurerCode.Should().Be(InsurerCode.Gnp);
        row.InsurerName.Should().Be("Grupo Nacional Provincial");
        row.InsurerLogoUrl.Should().Be("/img/logos/gnp.png");
    }

    [Fact]
    public async Task Result_view_includes_coverage_badges_from_PackageCoverageMatrix()
    {
        var vehicleId = Guid.NewGuid();
        var insurerId = Guid.NewGuid();
        var q = BuildQuotation(vehicleId, "{}");
        var result = QuotationInsurerResult.SucceededResult(
            q.Id, insurerId,
            premiumTotal: 1m, premiumNet: 1m, tax: 0m, fees: 0m,
            latencyMs: 1, externalQuoteRef: "Q-1",
            requestBlobRef: null, responseBlobRef: null, createdAt: NowUtc).Value;
        q.RecordResult(result);

        var qua = Insurer.Create(InsurerCode.Qua, "Quálitas", displayOrder: 2).Value;
        ForceId(qua, insurerId);

        _quotations.Setup(r => r.GetByIdAsync(q.Id, It.IsAny<CancellationToken>())).ReturnsAsync(q);
        _vehicles.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildVehicle(vehicleId));
        _insurers.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { qua });

        var handler = new GetQuotationStatus(_quotations.Object, _vehicles.Object, _insurers.Object);
        var view = await handler.ExecuteAsync(q.Id, CancellationToken.None);

        var row = view!.Results.Single();
        row.CoverageBadges.Should().HaveCount(3);
        row.CoverageBadges[0].Label.Should().Be("Protección Legal");
        row.CoverageBadges[1].Label.Should().Be("RC Ocupantes");
        row.CoverageBadges[2].Label.Should().Be("Asistencia Vial Plus");
        row.CoverageBadges.Should().AllSatisfy(b => b.Amparado.Should().BeTrue());
    }

    [Fact]
    public async Task Result_view_has_empty_coverage_badges_when_insurer_missing()
    {
        var vehicleId = Guid.NewGuid();
        var insurerId = Guid.NewGuid();
        var q = BuildQuotation(vehicleId, "{}");
        var result = QuotationInsurerResult.SucceededResult(
            q.Id, insurerId,
            premiumTotal: 1m, premiumNet: 1m, tax: 0m, fees: 0m,
            latencyMs: 1, externalQuoteRef: "X",
            requestBlobRef: null, responseBlobRef: null, createdAt: NowUtc).Value;
        q.RecordResult(result);

        _quotations.Setup(r => r.GetByIdAsync(q.Id, It.IsAny<CancellationToken>())).ReturnsAsync(q);
        _vehicles.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildVehicle(vehicleId));
        _insurers.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Insurer>());

        var handler = new GetQuotationStatus(_quotations.Object, _vehicles.Object, _insurers.Object);
        var view = await handler.ExecuteAsync(q.Id, CancellationToken.None);

        view!.Results.Single().CoverageBadges.Should().BeEmpty();
    }

    [Fact]
    public async Task Result_view_falls_back_to_null_insurer_fields_when_insurer_missing()
    {
        var vehicleId = Guid.NewGuid();
        var insurerId = Guid.NewGuid();
        var q = BuildQuotation(vehicleId, "{}");
        var result = QuotationInsurerResult.SucceededResult(
            q.Id, insurerId,
            premiumTotal: 0m, premiumNet: 0m, tax: 0m, fees: 0m,
            latencyMs: 0, externalQuoteRef: "?-001",
            requestBlobRef: null, responseBlobRef: null, createdAt: NowUtc).Value;
        q.RecordResult(result);

        _quotations.Setup(r => r.GetByIdAsync(q.Id, It.IsAny<CancellationToken>())).ReturnsAsync(q);
        _vehicles.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildVehicle(vehicleId));
        // Devolvemos lista vacía — el InsurerId del result no matchea ningún insurer.
        _insurers.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Insurer>());

        var handler = new GetQuotationStatus(_quotations.Object, _vehicles.Object, _insurers.Object);
        var view = await handler.ExecuteAsync(q.Id, CancellationToken.None);

        var row = view!.Results.Single();
        row.InsurerCode.Should().BeNull();
        row.InsurerName.Should().BeNull();
        row.InsurerLogoUrl.Should().BeNull();
    }

    [Fact]
    public async Task Result_view_prefers_explicit_LogoUrl_over_fallback()
    {
        var vehicleId = Guid.NewGuid();
        var insurerId = Guid.NewGuid();
        var q = BuildQuotation(vehicleId, "{}");
        var result = QuotationInsurerResult.SucceededResult(
            q.Id, insurerId,
            premiumTotal: 0m, premiumNet: 0m, tax: 0m, fees: 0m,
            latencyMs: 0, externalQuoteRef: "QUA-001",
            requestBlobRef: null, responseBlobRef: null, createdAt: NowUtc).Value;
        q.RecordResult(result);

        var qua = Insurer.Create(InsurerCode.Qua, "Quálitas", displayOrder: 2).Value;
        ForceId(qua, insurerId);
        var logoSet = qua.SetLogoUrl("https://cdn.example.com/logos/qualitas-official.svg");
        logoSet.IsSuccess.Should().BeTrue();

        _quotations.Setup(r => r.GetByIdAsync(q.Id, It.IsAny<CancellationToken>())).ReturnsAsync(q);
        _vehicles.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildVehicle(vehicleId));
        _insurers.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { qua });

        var handler = new GetQuotationStatus(_quotations.Object, _vehicles.Object, _insurers.Object);
        var view = await handler.ExecuteAsync(q.Id, CancellationToken.None);

        view!.Results.Single().InsurerLogoUrl.Should().Be("https://cdn.example.com/logos/qualitas-official.svg");
    }

    private static void ForceId(Insurer insurer, Guid id)
    {
        var idField = typeof(Insurer).GetField("<Id>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        idField!.SetValue(insurer, id);
    }
}
