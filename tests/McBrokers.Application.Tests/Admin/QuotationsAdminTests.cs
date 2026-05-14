using McBrokers.Application.Admin;
using McBrokers.Application.Ports;
using McBrokers.Domain.Catalog;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Quotations;

namespace McBrokers.Application.Tests.Admin;

public class QuotationsAdminTests
{
    private readonly Mock<IQuotationRepository> _quotations = new(MockBehavior.Strict);
    private readonly Mock<IVehicleMasterRepository> _vehicles = new(MockBehavior.Strict);
    private readonly Mock<IInsurerRepository> _insurers = new(MockBehavior.Strict);

    private static readonly DateTime NowUtc = DateTime.SpecifyKind(new(2026, 5, 14, 12, 0, 0), DateTimeKind.Utc);

    private static Quotation BuildQuotation(string corr, Guid vehicleId, Guid agentId)
    {
        return Quotation.Create(
            agentId, corr, vehicleId,
            PackageCode.Amplia, PaymentMode.Annual, ValuationType.Commercial,
            250_000m, "06700", "{}", NowUtc).Value;
    }

    private static VehicleMaster BuildVehicle(int year, string brand, string model, string version, Guid? id = null)
    {
        var v = VehicleMaster.Create(
            year, brand, model, version,
            "Sedán", VehicleTransmission.Automatic, 4, 4).Value;
        if (id is not null)
        {
            var idField = typeof(VehicleMaster).GetField("<Id>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            idField!.SetValue(v, id.Value);
        }
        return v;
    }

    [Fact]
    public async Task ListRecentQuotations_returns_paged_items_with_vehicle_summary()
    {
        var vId = Guid.NewGuid();
        var aId = Guid.NewGuid();
        var q1 = BuildQuotation("corr-1", vId, aId);
        var q2 = BuildQuotation("corr-2", vId, aId);

        _quotations.Setup(r => r.ListRecentAsync(25, 0, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new[] { q1, q2 });
        _quotations.Setup(r => r.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(42);
        _quotations.Setup(r => r.GetByIdAsync(q1.Id, It.IsAny<CancellationToken>())).ReturnsAsync(q1);
        _quotations.Setup(r => r.GetByIdAsync(q2.Id, It.IsAny<CancellationToken>())).ReturnsAsync(q2);
        _vehicles.Setup(r => r.GetByIdAsync(vId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(BuildVehicle(2024, "VW", "JETTA", "TRENDLINE", vId));

        var handler = new ListRecentQuotations(_quotations.Object, _vehicles.Object);
        var page = await handler.ExecuteAsync(page: 1, pageSize: 25, CancellationToken.None);

        page.Total.Should().Be(42);
        page.Items.Should().HaveCount(2);
        page.Items[0].VehicleSummary.Should().Be("2024 VW JETTA — TRENDLINE");
        page.Items[0].CorrelationId.Should().Be("corr-1");
    }

    [Fact]
    public async Task ListRecentQuotations_clamps_pageSize_to_reasonable_bounds()
    {
        _quotations.Setup(r => r.ListRecentAsync(200, 0, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Array.Empty<Quotation>());
        _quotations.Setup(r => r.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var handler = new ListRecentQuotations(_quotations.Object, _vehicles.Object);
        var page = await handler.ExecuteAsync(page: 1, pageSize: 99999, CancellationToken.None);

        page.PageSize.Should().Be(200, because: "pageSize is clamped to a hard ceiling of 200");
    }

    [Fact]
    public async Task ListRecentQuotations_uses_fallback_when_vehicle_missing()
    {
        var vId = Guid.NewGuid();
        var q = BuildQuotation("corr-x", vId, Guid.NewGuid());

        _quotations.Setup(r => r.ListRecentAsync(25, 0, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { q });
        _quotations.Setup(r => r.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _quotations.Setup(r => r.GetByIdAsync(q.Id, It.IsAny<CancellationToken>())).ReturnsAsync(q);
        _vehicles.Setup(r => r.GetByIdAsync(vId, It.IsAny<CancellationToken>())).ReturnsAsync((VehicleMaster?)null);

        var handler = new ListRecentQuotations(_quotations.Object, _vehicles.Object);
        var page = await handler.ExecuteAsync(1, 25, CancellationToken.None);

        page.Items.Single().VehicleSummary.Should().Contain("no disponible");
    }

    [Fact]
    public async Task GetQuotationAdminDetail_returns_null_for_missing_id()
    {
        var id = Guid.NewGuid();
        _quotations.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Quotation?)null);

        var handler = new GetQuotationAdminDetail(_quotations.Object, _vehicles.Object, _insurers.Object);
        var view = await handler.ExecuteAsync(id, CancellationToken.None);

        view.Should().BeNull();
    }

    [Fact]
    public async Task GetQuotationAdminDetail_includes_blob_refs_for_each_result()
    {
        var vId = Guid.NewGuid();
        var aId = Guid.NewGuid();
        var iId = Guid.NewGuid();
        var q = BuildQuotation("corr-detail", vId, aId);
        var r = QuotationInsurerResult.SucceededResult(
            q.Id, iId, 100m, 80m, 16m, 4m, 100, "qref",
            "file://path/req.xml", "file://path/res.xml", NowUtc).Value;
        q.RecordResult(r);

        var insurer = Insurer.Create(InsurerCode.Gnp, "GNP", 1).Value;
        var idField = typeof(Insurer).GetField("<Id>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        idField!.SetValue(insurer, iId);

        _quotations.Setup(x => x.GetByIdAsync(q.Id, It.IsAny<CancellationToken>())).ReturnsAsync(q);
        _vehicles.Setup(x => x.GetByIdAsync(vId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(BuildVehicle(2024, "VW", "JETTA", "TRENDLINE", vId));
        _insurers.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { insurer });

        var handler = new GetQuotationAdminDetail(_quotations.Object, _vehicles.Object, _insurers.Object);
        var view = await handler.ExecuteAsync(q.Id, CancellationToken.None);

        view!.Results.Should().HaveCount(1);
        view.Results[0].InsurerName.Should().Be("GNP");
        view.Results[0].RequestBlobRef.Should().Be("file://path/req.xml");
        view.Results[0].ResponseBlobRef.Should().Be("file://path/res.xml");
        view.Results[0].IsCurrent.Should().BeTrue();
    }
}
