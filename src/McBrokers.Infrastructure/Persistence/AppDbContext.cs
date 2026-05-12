using McBrokers.Domain.Agents;
using McBrokers.Domain.Audit;
using McBrokers.Domain.Catalog;
using McBrokers.Domain.Emissions;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Insurers.AxaDxn;
using McBrokers.Application.Ports;
using McBrokers.Domain.Quotations;
using Microsoft.EntityFrameworkCore;

namespace McBrokers.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly IPasswordProtector? _passwordProtector;

    public AppDbContext(DbContextOptions<AppDbContext> options, IPasswordProtector passwordProtector)
        : base(options)
    {
        _passwordProtector = passwordProtector;
    }

    // Ctor para EF Tools (dotnet ef migrations / database update). El protector no se usa
    // durante migraciones — los converter lambdas no se ejecutan al generar SQL.
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        _passwordProtector = null;
    }

    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Insurer> Insurers => Set<Insurer>();
    public DbSet<InsurerConfig> InsurerConfigs => Set<InsurerConfig>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();
    public DbSet<VehicleMaster> VehicleMasters => Set<VehicleMaster>();
    public DbSet<VehicleInsurerMapping> VehicleInsurerMappings => Set<VehicleInsurerMapping>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<BrandSynonym> BrandSynonyms => Set<BrandSynonym>();
    public DbSet<TransmissionSynonym> TransmissionSynonyms => Set<TransmissionSynonym>();
    public DbSet<CatalogImportBatch> CatalogImportBatches => Set<CatalogImportBatch>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationInsurerResult> QuotationInsurerResults => Set<QuotationInsurerResult>();
    public DbSet<KnownInsurerError> KnownInsurerErrors => Set<KnownInsurerError>();
    public DbSet<InsurerPackageMapping> InsurerPackageMappings => Set<InsurerPackageMapping>();
    public DbSet<Emission> Emissions => Set<Emission>();
    public DbSet<EmissionAttempt> EmissionAttempts => Set<EmissionAttempt>();
    public DbSet<AxaDxnConfig> AxaDxnConfigs => Set<AxaDxnConfig>();
    public DbSet<AxaDxnBusiness> AxaDxnBusinesses => Set<AxaDxnBusiness>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Cifrado columna-a-columna para passwords de aseguradoras. El converter aplica
        // protect/unprotect transparente: Domain trabaja con plaintext, BD almacena cipher.
        // En tools EF (sin protector) el modelo se construye igual — el converter solo
        // corre durante materialización/persistencia, no en generación de SQL DDL.
        if (_passwordProtector is not null)
        {
            var protector = _passwordProtector;
            modelBuilder.Entity<Domain.Insurers.AxaDxn.AxaDxnConfig>()
                .Property(c => c.Password)
                .HasConversion(
                    plain => protector.Protect(plain),
                    cipher => protector.Unprotect(cipher));
        }
    }
}
