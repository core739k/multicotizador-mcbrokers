using McBrokers.Application.Admin;
using McBrokers.Application.Auth;
using McBrokers.Application.Ports;
using McBrokers.Infrastructure.Audit;
using McBrokers.Infrastructure.Identity;
using McBrokers.Infrastructure.Persistence;
using McBrokers.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace McBrokers.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMcBrokersInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Missing connection string 'Default' (use Key Vault for production).");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddHttpContextAccessor();

        // Repositories
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IInsurerRepository, InsurerRepository>();
        services.AddScoped<IInsurerConfigRepository, InsurerConfigRepository>();

        // Cross-cutting
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentAgentProvider, HttpContextCurrentAgentProvider>();
        services.AddScoped<IAuditWriter, AuditWriter>();

        // Use cases
        services.AddScoped<ResolveAgentFromGoogleToken>();
        services.AddScoped<CreateInsurer>();
        services.AddScoped<UpdateInsurer>();
        services.AddScoped<UpsertInsurerConfig>();
        services.AddScoped<ListInsurers>();
        services.AddScoped<GetInsurer>();
        services.AddScoped<ListAgents>();
        services.AddScoped<UpdateAgentRole>();
        services.AddScoped<SetAgentActive>();

        services.AddMcBrokersGoogleAuthentication(configuration);
        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAdmin", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim(System.Security.Claims.ClaimTypes.Role, nameof(McBrokers.Domain.Agents.AgentRole.Admin)));
        });

        return services;
    }
}
