using McBrokers.Application.Catalog;
using McBrokers.Application.Ports;

namespace McBrokers.Application.Tests.Catalog;

public class SearchVehiclesByTextTests
{
    private readonly Mock<ISearchVehiclesByTextRepository> _repo = new(MockBehavior.Strict);

    private static readonly Guid InsurerA = Guid.NewGuid();
    private static readonly Guid InsurerB = Guid.NewGuid();

    [Fact]
    public async Task Empty_query_returns_empty_results_and_does_not_hit_repository()
    {
        var handler = new SearchVehiclesByText(_repo.Object);

        var result = await handler.ExecuteAsync(2024, "", new[] { InsurerA }, CancellationToken.None);

        result.Items.Should().BeEmpty();
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Whitespace_query_returns_empty_results_and_does_not_hit_repository()
    {
        var handler = new SearchVehiclesByText(_repo.Object);

        var result = await handler.ExecuteAsync(2024, "   ", new[] { InsurerA }, CancellationToken.None);

        result.Items.Should().BeEmpty();
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Query_is_tokenized_by_whitespace_dropping_empties()
    {
        VehicleSearchCriteria? captured = null;
        _repo.Setup(r => r.SearchAsync(It.IsAny<VehicleSearchCriteria>(), It.IsAny<CancellationToken>()))
             .Callback<VehicleSearchCriteria, CancellationToken>((c, _) => captured = c)
             .ReturnsAsync(Array.Empty<VehicleSearchHit>());

        var handler = new SearchVehiclesByText(_repo.Object);
        await handler.ExecuteAsync(2024, "  VW   JETTA  2024  ", new[] { InsurerA }, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Tokens.Should().Equal("VW", "JETTA", "2024");
        captured.Year.Should().Be(2024);
        captured.InsurerIds.Should().Equal(InsurerA);
    }

    [Fact]
    public async Task Returns_hits_projected_with_display_string()
    {
        var masterId = Guid.NewGuid();
        _repo.Setup(r => r.SearchAsync(It.IsAny<VehicleSearchCriteria>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new[]
             {
                 new VehicleSearchHit(masterId, 2024, "VW", "JETTA", "TRENDLINE", new[] { InsurerA, InsurerB }),
             });

        var handler = new SearchVehiclesByText(_repo.Object);
        var result = await handler.ExecuteAsync(2024, "jetta", new[] { InsurerA, InsurerB }, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].VehicleMasterId.Should().Be(masterId);
        result.Items[0].Year.Should().Be(2024);
        result.Items[0].Display.Should().Be("2024 VW JETTA — TRENDLINE");
        result.Items[0].AvailableInsurerIds.Should().Equal(InsurerA, InsurerB);
    }

    [Fact]
    public async Task Marks_partial_availability_when_some_insurers_have_no_approved_mapping()
    {
        // Vendedor selecciona A y B; el vehículo solo tiene mapping aprobado para A.
        // Filtro permisivo: el resultado se devuelve, marcando que solo A está disponible.
        var masterId = Guid.NewGuid();
        _repo.Setup(r => r.SearchAsync(It.IsAny<VehicleSearchCriteria>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new[]
             {
                 new VehicleSearchHit(masterId, 2024, "VW", "JETTA", "TRENDLINE", new[] { InsurerA }),
             });

        var handler = new SearchVehiclesByText(_repo.Object);
        var result = await handler.ExecuteAsync(2024, "jetta", new[] { InsurerA, InsurerB }, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].AvailableInsurerIds.Should().Equal(InsurerA);
    }

    [Fact]
    public async Task Returns_empty_when_repository_returns_no_hits()
    {
        _repo.Setup(r => r.SearchAsync(It.IsAny<VehicleSearchCriteria>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(Array.Empty<VehicleSearchHit>());

        var handler = new SearchVehiclesByText(_repo.Object);
        var result = await handler.ExecuteAsync(2024, "zorro fantasma", new[] { InsurerA }, CancellationToken.None);

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Tolerates_null_insurerIds_by_treating_as_empty_selection()
    {
        VehicleSearchCriteria? captured = null;
        _repo.Setup(r => r.SearchAsync(It.IsAny<VehicleSearchCriteria>(), It.IsAny<CancellationToken>()))
             .Callback<VehicleSearchCriteria, CancellationToken>((c, _) => captured = c)
             .ReturnsAsync(Array.Empty<VehicleSearchHit>());

        var handler = new SearchVehiclesByText(_repo.Object);
        await handler.ExecuteAsync(2024, "jetta", insurerIds: null!, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.InsurerIds.Should().BeEmpty();
    }
}
