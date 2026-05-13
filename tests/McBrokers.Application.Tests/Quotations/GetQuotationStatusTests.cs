using McBrokers.Application.Ports;
using McBrokers.Application.Quotations;
using McBrokers.Domain.Catalog;
using McBrokers.Domain.Quotations;

namespace McBrokers.Application.Tests.Quotations;

public class GetQuotationStatusTests
{
    private readonly Mock<IQuotationRepository> _quotations = new(MockBehavior.Strict);
    private readonly Mock<IVehicleMasterRepository> _vehicles = new(MockBehavior.Strict);

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

        var handler = new GetQuotationStatus(_quotations.Object, _vehicles.Object);
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

        var handler = new GetQuotationStatus(_quotations.Object, _vehicles.Object);
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

        var handler = new GetQuotationStatus(_quotations.Object, _vehicles.Object);
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

        var handler = new GetQuotationStatus(_quotations.Object, _vehicles.Object);
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

        var handler = new GetQuotationStatus(_quotations.Object, _vehicles.Object);
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

        var handler = new GetQuotationStatus(_quotations.Object, _vehicles.Object);
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

        var handler = new GetQuotationStatus(_quotations.Object, _vehicles.Object);
        var view = await handler.ExecuteAsync(q.Id, CancellationToken.None);

        view!.Deducibles.Should().BeNull(because: "the view should not crash on bad snapshots");
    }
}
