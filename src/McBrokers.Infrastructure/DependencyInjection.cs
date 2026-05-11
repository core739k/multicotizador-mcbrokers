using McBrokers.Application.Admin;
using McBrokers.Application.Auth;
using McBrokers.Application.Catalog;
using McBrokers.Application.Ports;
using McBrokers.Domain.Catalog.Matching;
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
        services.AddScoped<IVehicleMasterRepository, VehicleMasterRepository>();
        services.AddScoped<IVehicleInsurerMappingRepository, VehicleInsurerMappingRepository>();
        services.AddScoped<ICatalogImportBatchRepository, CatalogImportBatchRepository>();

        // Cross-cutting
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentAgentProvider, HttpContextCurrentAgentProvider>();
        services.AddScoped<IAuditWriter, AuditWriter>();

        // Catalog services (domain)
        services.AddSingleton<TextNormalizer>(_ => new TextNormalizer(BuiltInSynonyms));

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
        services.AddScoped<ImportInsurerCatalog>();
        services.AddScoped<DecideMapping>();
        services.AddScoped<GetCatalogForYear>();
        services.AddScoped<ListPendingMappings>();

        services.AddMcBrokersGoogleAuthentication(configuration);
        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAdmin", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim(System.Security.Claims.ClaimTypes.Role, nameof(McBrokers.Domain.Agents.AgentRole.Admin)));
        });

        return services;
    }

    // Sinónimos básicos del README_multicotizadorminero. Los administrables (BrandSynonym /
    // TransmissionSynonym en BD) se inyectarán como overlay desde el repo en una iteración futura.
    private static readonly IReadOnlyDictionary<string, string> BuiltInSynonyms = new Dictionary<string, string>
    {
        ["STD"] = "ESTANDAR",
        ["AUT"] = "AUTOMATICO",
        ["MAN"] = "ESTANDAR",
        ["C/A"] = "AC",
        ["4CIL"] = "4 CILINDROS",
        ["6CIL"] = "6 CILINDROS",
        ["8CIL"] = "8 CILINDROS",
        ["CIL"] = "CILINDROS",
        ["AA"] = "AC",
    };
}
