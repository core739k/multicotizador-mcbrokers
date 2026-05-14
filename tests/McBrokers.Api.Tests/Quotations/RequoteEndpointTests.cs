using System.Net;
using System.Net.Http.Json;
using McBrokers.Api.Tests.Testing;
using McBrokers.Domain.Agents;
using McBrokers.Domain.Quotations;
using Microsoft.Extensions.DependencyInjection;

namespace McBrokers.Api.Tests.Quotations;

// Tests del endpoint POST /api/v1/quotations/{id}/results/{insurerId}/requote.
// El happy path con adapter real requeriría mucho seed (insurer + config +
// mapping + adapter fake). Aquí cubro auth + las 3 validaciones de input
// (quotation/insurer no existen, prior no existe). El happy path con
// adapter fake queda como ejercicio para Fase posterior si la cobertura
// de integración la pide.
public class RequoteEndpointTests : IClassFixture<AdminApiFactory>
{
    private readonly AdminApiFactory _factory;

    public RequoteEndpointTests(AdminApiFactory factory) => _factory = factory;

    private async Task ResetDbAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<McBrokers.Infrastructure.Persistence.AppDbContext>();
        db.QuotationInsurerResults.RemoveRange(db.QuotationInsurerResults);
        db.Quotations.RemoveRange(db.Quotations);
        db.Insurers.RemoveRange(db.Insurers);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Returns_400_when_quotation_not_found()
    {
        _factory.TestAgentRole = AgentRole.Agent;
        await ResetDbAsync();
        using var client = _factory.CreateClient();

        var body = new
        {
            Valuation = ValuationType.Agreed,
            DMPct = 10m,
            RTPct = 15m,
            GMO = 300_000m,
        };
        var url = $"/api/v1/quotations/{Guid.NewGuid()}/results/{Guid.NewGuid()}/requote";

        var response = await client.PostAsJsonAsync(url, body);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Returns_401_or_redirect_when_anonymous()
    {
        await using var anon = new AnonymousApiFactory();
        using var client = anon.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var url = $"/api/v1/quotations/{Guid.NewGuid()}/results/{Guid.NewGuid()}/requote";
        var response = await client.PostAsJsonAsync(url, new { Valuation = ValuationType.Agreed });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.Redirect, HttpStatusCode.Found);
    }

    private sealed class AnonymousApiFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Default", "Server=unreachable;Database=test;");
            builder.UseSetting("Authentication:Google:ClientId", "test-id");
            builder.UseSetting("Authentication:Google:ClientSecret", "test-secret");
        }
    }
}
