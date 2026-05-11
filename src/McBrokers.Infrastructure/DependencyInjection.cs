using McBrokers.Application.Auth;
using McBrokers.Application.Ports;
using McBrokers.Infrastructure.Identity;
using McBrokers.Infrastructure.Persistence;
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

        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<ResolveAgentFromGoogleToken>();

        services.AddMcBrokersGoogleAuthentication(configuration);
        services.AddAuthorization();

        return services;
    }
}
