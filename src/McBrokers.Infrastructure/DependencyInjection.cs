using McBrokers.Application.Admin;
using McBrokers.Application.Auth;
using McBrokers.Application.Catalog;
using McBrokers.Application.Ports;
using McBrokers.Application.Quotations;
using McBrokers.Domain.Catalog.Matching;
using McBrokers.Domain.Insurers;
using McBrokers.Infrastructure.Audit;
using McBrokers.Infrastructure.Blob;
using McBrokers.Infrastructure.Identity;
using McBrokers.Infrastructure.Messaging;
using McBrokers.Infrastructure.Persistence;
using McBrokers.Infrastructure.Time;
using McBrokers.Insurers.Abstractions;
using McBrokers.Insurers.Ana;
using McBrokers.Insurers.AxaCol;
using McBrokers.Insurers.AxaDxn;
using McBrokers.Insurers.Gnp;
using McBrokers.Insurers.Qualitas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
        services.AddScoped<IQuotationRepository, QuotationRepository>();
        services.AddScoped<IInsurerPackageMappingRepository, InsurerPackageMappingRepository>();
        services.AddScoped<IInsurerCredentialProvider, KeyVaultCredentialProvider>();
        services.AddScoped<IEmissionRepository, EmissionRepository>();

        // Email + PDF download (dev impls). Producción: Microsoft Graph/SendGrid + HttpClient resiliente.
        services.AddScoped<IEmailSender, Email.LogEmailSender>();
        services.AddHttpClient<Pdf.HttpPdfDownloader>();
        services.AddScoped<IPdfDownloader, Pdf.HttpPdfDownloader>();

        // Blob (LocalDisk en dev; en prod el path llega de configuración o se sustituye por AzureBlobStore).
        var blobRoot = configuration["Blob:LocalRoot"] ?? Path.Combine(Path.GetTempPath(), "mcbrokers-blobs");
        services.AddSingleton<IBlobStore>(sp =>
            new LocalDiskBlobStore(blobRoot, sp.GetRequiredService<ILogger<LocalDiskBlobStore>>()));

        // Cola y worker
        services.AddSingleton<IQuotationQueue, InMemoryQuotationQueue>();
        services.AddHostedService<QuotationWorker>();
        // Orden importante: InsurersSeed antes que KnownInsurerErrorsSeed (este último depende
        // de que existan las aseguradoras para enlazar errores por InsurerId).
        services.AddHostedService<Startup.InsurersSeed>();
        services.AddHostedService<Startup.KnownInsurerErrorsSeed>();

        // Adapters de aseguradora. Cada uno con su HttpClient nombrado para timeouts/handlers propios.
        services.AddHttpClient<GnpQuoteAdapter>();
        services.AddHttpClient<QualitasQuoteAdapter>();
        services.AddHttpClient<AnaQuoteAdapter>();
        services.AddHttpClient<AnaPostalCodeResolver>();
        services.AddMemoryCache();
        services.AddScoped<IAnaPostalCodeResolver, AnaPostalCodeResolver>();
        services.AddHttpClient<AxaColQuoteAdapter>();
        services.AddHttpClient<AxaDxnQuoteAdapter>();
        services.AddScoped<IInsurerAdapter, GnpQuoteAdapter>();
        services.AddScoped<IInsurerAdapter, QualitasQuoteAdapter>();
        services.AddScoped<IInsurerAdapter, AnaQuoteAdapter>();
        services.AddScoped<IInsurerAdapter, AxaColQuoteAdapter>();
        services.AddScoped<IInsurerAdapter, AxaDxnQuoteAdapter>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);

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
        services.AddScoped<UpsertInsurerPackageMapping>();
        services.AddScoped<ListInsurers>();
        services.AddScoped<GetInsurer>();
        services.AddScoped<ListAgents>();
        services.AddScoped<UpdateAgentRole>();
        services.AddScoped<SetAgentActive>();
        services.AddScoped<ImportInsurerCatalog>();
        services.AddScoped<DecideMapping>();
        services.AddScoped<GetCatalogForYear>();
        services.AddScoped<ListPendingMappings>();
        services.AddScoped<RequestQuotation>();
        services.AddScoped<GetQuotationStatus>();
        services.AddScoped<ProcessQuotation>();
        services.AddScoped<McBrokers.Application.Emissions.EmitPolicy>();

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
