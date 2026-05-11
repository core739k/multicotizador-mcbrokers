using McBrokers.Domain.Agents;
using McBrokers.Domain.Audit;
using McBrokers.Domain.Catalog;
using McBrokers.Domain.Insurers;
using Microsoft.EntityFrameworkCore;

namespace McBrokers.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
