using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace McBrokers.Api.Tests.HealthChecks;

public class HealthCheckEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public HealthCheckEndpointsTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Live_returns_200_even_when_database_is_unreachable()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.IsSuccessStatusCode.Should().BeTrue(
            "/health/live must only verify the host is up, never dependencies");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("Healthy");
    }
}

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", "Server=unreachable;Database=test;");
        builder.UseSetting("Authentication:Google:ClientId", "test-client-id");
        builder.UseSetting("Authentication:Google:ClientSecret", "test-client-secret");
    }
}
