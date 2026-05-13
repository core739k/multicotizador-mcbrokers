using System.Net;
using System.Net.Http.Json;
using McBrokers.Api.Tests.Testing;
using McBrokers.Domain.Agents;
using McBrokers.Domain.Catalog;
using McBrokers.Domain.Insurers;
using McBrokers.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace McBrokers.Api.Tests.Catalog;

public class CatalogSearchEndpointsTests : IClassFixture<AdminApiFactory>
{
    private readonly AdminApiFactory _factory;

    public CatalogSearchEndpointsTests(AdminApiFactory factory) => _factory = factory;

    private sealed record SeedHandles(Guid InsurerAId, Guid InsurerBId, Guid JettaId, Guid SentraId);

    private async Task<SeedHandles> SeedCatalogAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Reset relevant tables.
        db.VehicleInsurerMappings.RemoveRange(db.VehicleInsurerMappings);
        db.VehicleMasters.RemoveRange(db.VehicleMasters);
        db.Insurers.RemoveRange(db.Insurers);
        await db.SaveChangesAsync();

        var insurerA = Insurer.Create(InsurerCode.Gnp, "Aseguradora A", displayOrder: 1).Value;
        var insurerB = Insurer.Create(InsurerCode.Qua, "Aseguradora B", displayOrder: 2).Value;
        db.Insurers.AddRange(insurerA, insurerB);

        var jetta = VehicleMaster.Create(2024, "VOLKSWAGEN", "JETTA", "TRENDLINE",
            "Sedán", VehicleTransmission.Automatic, 4, 4).Value;
        var sentra = VehicleMaster.Create(2024, "NISSAN", "SENTRA", "ADVANCE",
            "Sedán", VehicleTransmission.Automatic, 4, 4).Value;
        var oldJetta = VehicleMaster.Create(2020, "VOLKSWAGEN", "JETTA", "GLI",
            "Sedán", VehicleTransmission.Automatic, 4, 4).Value;
        db.VehicleMasters.AddRange(jetta, sentra, oldJetta);

        // Jetta tiene mapping aprobado en A y B; Sentra solo en A; oldJetta en A.
        var nowUtc = DateTime.UtcNow;
        db.VehicleInsurerMappings.AddRange(
            VehicleInsurerMapping.Create(jetta.Id, insurerA.Id, "JE-A-1", "VW", "JETTA", "TRENDLINE", 98m, nowUtc).Value,
            VehicleInsurerMapping.Create(jetta.Id, insurerB.Id, "JE-B-1", "VW", "JETTA", "TRENDLINE", 96m, nowUtc).Value,
            VehicleInsurerMapping.Create(sentra.Id, insurerA.Id, "SE-A-1", "NISSAN", "SENTRA", "ADVANCE", 97m, nowUtc).Value,
            VehicleInsurerMapping.Create(oldJetta.Id, insurerA.Id, "OJ-A-1", "VW", "JETTA", "GLI", 99m, nowUtc).Value);

        await db.SaveChangesAsync();
        return new SeedHandles(insurerA.Id, insurerB.Id, jetta.Id, sentra.Id);
    }

    [Fact]
    public async Task Returns_matching_vehicles_filtered_by_year_and_token()
    {
        _factory.TestAgentRole = AgentRole.Agent;
        var seed = await SeedCatalogAsync();
        using var client = _factory.CreateClient();

        var url = $"/api/v1/catalog/search?q=jetta&year=2024&insurerIds={seed.InsurerAId}&insurerIds={seed.InsurerBId}";
        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<VehicleSearchPayload>();
        body!.Items.Should().HaveCount(1, because: "the 2020 Jetta is filtered out by year");
        body.Items[0].VehicleMasterId.Should().Be(seed.JettaId);
        body.Items[0].Display.Should().Contain("JETTA").And.Contain("TRENDLINE");
        body.Items[0].AvailableInsurerIds.Should().BeEquivalentTo(new[] { seed.InsurerAId, seed.InsurerBId });
    }

    [Fact]
    public async Task Marks_partial_availability_when_only_some_insurers_have_mapping()
    {
        _factory.TestAgentRole = AgentRole.Agent;
        var seed = await SeedCatalogAsync();
        using var client = _factory.CreateClient();

        var url = $"/api/v1/catalog/search?q=sentra&year=2024&insurerIds={seed.InsurerAId}&insurerIds={seed.InsurerBId}";
        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<VehicleSearchPayload>();
        body!.Items.Should().HaveCount(1);
        body.Items[0].AvailableInsurerIds.Should().Equal(seed.InsurerAId);
    }

    [Fact]
    public async Task Returns_empty_when_no_vehicle_matches()
    {
        _factory.TestAgentRole = AgentRole.Agent;
        var seed = await SeedCatalogAsync();
        using var client = _factory.CreateClient();

        var url = $"/api/v1/catalog/search?q=zorrofantasma&year=2024&insurerIds={seed.InsurerAId}";
        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<VehicleSearchPayload>();
        body!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_empty_when_query_is_blank()
    {
        _factory.TestAgentRole = AgentRole.Agent;
        var seed = await SeedCatalogAsync();
        using var client = _factory.CreateClient();

        var url = $"/api/v1/catalog/search?q=&year=2024&insurerIds={seed.InsurerAId}";
        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<VehicleSearchPayload>();
        body!.Items.Should().BeEmpty();
    }

    private sealed record VehicleSearchPayload(IReadOnlyList<VehicleSearchItem> Items);
    private sealed record VehicleSearchItem(Guid VehicleMasterId, int Year, string Display, IReadOnlyList<Guid> AvailableInsurerIds);
}
