using System.Security.Claims;
using System.Text.Encodings.Web;
using McBrokers.Domain.Agents;
using McBrokers.Infrastructure.Identity;
using McBrokers.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McBrokers.Api.Tests.Testing;

public sealed class AdminApiFactory : WebApplicationFactory<Program>
{
    public Guid TestAgentId { get; } = Guid.NewGuid();
    public AgentRole TestAgentRole { get; set; } = AgentRole.Admin;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", "Server=(unused);Database=test;");
        builder.UseSetting("Authentication:Google:ClientId", "test-id");
        builder.UseSetting("Authentication:Google:ClientSecret", "test-secret");

        builder.ConfigureServices(services =>
        {
            // Replace SQL Server with EF InMemory using an isolated internal service
            // provider so EF doesn't see both providers in the global container.
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(AppDbContext))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            var efInternalSp = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            // Single shared in-memory DB across scopes — captured outside the lambda
            // so every DbContext built per request sees the same store.
            var dbName = $"admin-tests-{Guid.NewGuid()}";

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInternalServiceProvider(efInternalSp);
                options.UseInMemoryDatabase(dbName);
            });

            // Replace auth with a deterministic test scheme that issues an Admin principal.
            services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.Configure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            });

            // Make the test agent id and role discoverable from the auth handler.
            services.AddSingleton<TestAgentContext>(_ => new TestAgentContext(TestAgentId, () => TestAgentRole));

            // Quitar los HostedServices de seed para que los tests E2E partan de BD limpia.
            // Cuando un test específico requiera seed, lo inyecta manualmente vía AppDbContext.
            var hostedToRemove = services
                .Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                            && d.ImplementationType is not null
                            && (d.ImplementationType.Name == "InsurersSeed"
                                || d.ImplementationType.Name == "KnownInsurerErrorsSeed"
                                || d.ImplementationType.Name == "QuotationWorker"))
                .ToList();
            foreach (var d in hostedToRemove) services.Remove(d);
        });
    }
}

public sealed record TestAgentContext(Guid AgentId, Func<AgentRole> ResolveRole);

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";

    private readonly TestAgentContext _agent;

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestAgentContext agent)
        : base(options, logger, encoder)
    {
        _agent = agent;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _agent.AgentId.ToString()),
            new Claim(ClaimTypes.Email, "admin@mcbrokers.com.mx"),
            new Claim(ClaimTypes.Role, _agent.ResolveRole().ToString()),
            new Claim(HttpContextCurrentAgentProvider.AgentIdClaim, _agent.AgentId.ToString()),
            new Claim("mcb:full-name", "Test Admin"),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

