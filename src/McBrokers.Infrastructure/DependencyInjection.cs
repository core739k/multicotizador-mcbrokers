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
using McBrokers.Infrastructure.Observability;
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
        services.AddScoped<IKnownInsurerErrorLookup, KnownInsurerErrorLookup>();

        // Email + PDF download (dev impls). Producción: Microsoft Graph/SendGrid + HttpClient resiliente.
        services.AddScoped<IEmailSender, Email.LogEmailSender>();
        services.AddHttpClient<Pdf.HttpPdfDownloader>();
        services.AddScoped<IPdfDownloader, Pdf.HttpPdfDownloader>();

        // Blob: si hay Storage:ConnectionString → AzureBlobStore; si no → LocalDiskBlobStore (dev).
        var azureBlobConn = configuration["Storage:ConnectionString"]
                         ?? configuration.GetConnectionString("AzureBlob");
        if (!string.IsNullOrWhiteSpace(azureBlobConn))
        {
            var createOnDemand = configuration.GetValue<bool>("Storage:CreateContainerOnDemand");
            services.AddSingleton(new Azure.Storage.Blobs.BlobServiceClient(azureBlobConn));
            services.AddSingleton<IBlobStore>(sp => new AzureBlobStore(
                sp.GetRequiredService<Azure.Storage.Blobs.BlobServiceClient>(),
                sp.GetRequiredService<ILogger<AzureBlobStore>>(),
                createOnDemand));
        }
        else
        {
            var blobRoot = configuration["Blob:LocalRoot"]
                ?? Path.Combine(Path.GetTempPath(), "mcbrokers-blobs");
            services.AddSingleton<IBlobStore>(sp =>
                new LocalDiskBlobStore(blobRoot, sp.GetRequiredService<ILogger<LocalDiskBlobStore>>()));
        }

        // Cola y worker
        services.AddSingleton<IQuotationQueue, InMemoryQuotationQueue>();
        services.AddHostedService<QuotationWorker>();
        // Orden importante: InsurersSeed antes que KnownInsurerErrorsSeed (este último depende
        // de que existan las aseguradoras para enlazar errores por InsurerId).
        services.AddHostedService<Startup.InsurersSeed>();
        services.AddHostedService<Startup.KnownInsurerErrorsSeed>();

        // Adapters de aseguradora. Cada HttpClient con su política de resilience (Polly v8):
        // - 3 reintentos exponenciales sobre 5xx, timeouts y errores de red.
        // - Circuit breaker que abre tras 5 fallos consecutivos en 30s y se cierra después de 60s.
        services.AddHttpClient<GnpQuoteAdapter>().AddMcBrokersResilience();
        services.AddHttpClient<QualitasQuoteAdapter>().AddMcBrokersResilience();
        services.AddHttpClient<AnaQuoteAdapter>().AddMcBrokersResilience();
        services.AddHttpClient<AnaPostalCodeResolver>().AddMcBrokersResilience();
        services.AddMemoryCache();
        services.AddScoped<IAnaPostalCodeResolver, AnaPostalCodeResolver>();
        services.AddHttpClient<AxaColQuoteAdapter>().AddMcBrokersResilience();
        services.AddHttpClient<AxaDxnQuoteAdapter>().AddMcBrokersResilience();
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

            options.AddPolicy("RequireTechnicalAdmin", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim(System.Security.Claims.ClaimTypes.Role, nameof(McBrokers.Domain.Agents.AgentRole.Admin))
                      .RequireClaim("mcb:is-technical", "true"));
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
