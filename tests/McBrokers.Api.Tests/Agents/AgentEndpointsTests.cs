using System.Net;
using System.Net.Http.Json;
using McBrokers.Api.Tests.Testing;
using McBrokers.Domain.Agents;
using McBrokers.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace McBrokers.Api.Tests.Agents;

public class AgentEndpointsTests : IClassFixture<AdminApiFactory>
{
    private readonly AdminApiFactory _factory;

    public AgentEndpointsTests(AdminApiFactory factory) => _factory = factory;

    private async Task SeedTestAgentAsync(string fullName, string? agentCode)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Wipe any prior fixture state for this agent id.
        var existing = await db.Agents.FindAsync(_factory.TestAgentId);
        if (existing is not null)
        {
            db.Agents.Remove(existing);
            await db.SaveChangesAsync();
        }
        var email = AgentEmail.Create("admin@mcbrokers.com.mx").Value;
        var agent = Agent.Create(email, fullName, AgentRole.Admin, agentCode).Value;
        // Force the test agent id so the auth claim matches.
        var idField = typeof(Agent).GetField("<Id>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        idField!.SetValue(agent, _factory.TestAgentId);
        db.Agents.Add(agent);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Returns_view_for_authenticated_agent_with_code()
    {
        _factory.TestAgentRole = AgentRole.Admin;
        await SeedTestAgentAsync("Esteban Contreras", "MCB-001");
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/agent/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CurrentAgentResponse>();
        body!.FullName.Should().Be("Esteban Contreras");
        body.AgentCode.Should().Be("MCB-001");
    }

    [Fact]
    public async Task Returns_view_with_null_code_when_agent_has_no_code()
    {
        _factory.TestAgentRole = AgentRole.Agent;
        await SeedTestAgentAsync("Sin Clave", agentCode: null);
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/agent/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CurrentAgentResponse>();
        body!.FullName.Should().Be("Sin Clave");
        body.AgentCode.Should().BeNull();
    }

    [Fact]
    public async Task Returns_401_when_anonymous()
    {
        // Use a factory without the test auth scheme to verify the endpoint
        // actually requires authentication.
        await using var anonymousFactory = new AnonymousApiFactory();
        using var client = anonymousFactory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/agent/me");

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Redirect,
            HttpStatusCode.Found);
    }

    private sealed record CurrentAgentResponse(string FullName, string? AgentCode, string? PhotoUrl);

    private sealed class AnonymousApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Default", "Server=unreachable;Database=test;");
            builder.UseSetting("Authentication:Google:ClientId", "test-id");
            builder.UseSetting("Authentication:Google:ClientSecret", "test-secret");
        }
    }
}
