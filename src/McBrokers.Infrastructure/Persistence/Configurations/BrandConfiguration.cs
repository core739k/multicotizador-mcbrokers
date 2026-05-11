using McBrokers.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McBrokers.Infrastructure.Persistence.Configurations;

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.CanonicalName).HasMaxLength(100).IsRequired();
        builder.HasIndex(b => b.CanonicalName).IsUnique().HasDatabaseName("UX_Brands_CanonicalName");
    }
}

public class BrandSynonymConfiguration : IEntityTypeConfiguration<BrandSynonym>
{
    public void Configure(EntityTypeBuilder<BrandSynonym> builder)
    {
        builder.ToTable("BrandSynonyms");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.BrandId).IsRequired();
        builder.Property(s => s.SynonymText).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Source).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => s.SynonymText).HasDatabaseName("IX_BrandSynonyms_SynonymText");
        builder.HasOne<Brand>().WithMany().HasForeignKey(s => s.BrandId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TransmissionSynonymConfiguration : IEntityTypeConfiguration<TransmissionSynonym>
{
    public void Configure(EntityTypeBuilder<TransmissionSynonym> builder)
    {
        builder.ToTable("TransmissionSynonyms");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Text).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Canonical).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(s => s.Text).IsUnique().HasDatabaseName("UX_TransmissionSynonyms_Text");
    }
}

public class CatalogImportBatchConfiguration : IEntityTypeConfiguration<CatalogImportBatch>
{
    public void Configure(EntityTypeBuilder<CatalogImportBatch> builder)
    {
        builder.ToTable("CatalogImportBatches");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.InsurerId).IsRequired();
        builder.Property(b => b.Source).HasMaxLength(500).IsRequired();
        builder.Property(b => b.StartedAt).IsRequired();
        builder.Property(b => b.CompletedAt);
        builder.Property(b => b.RowsTotal);
        builder.Property(b => b.RowsAutoApproved);
        builder.Property(b => b.RowsPendingReview);
        builder.Property(b => b.RowsRejected);
        builder.Property(b => b.ImportedByAgentId).IsRequired();
        builder.HasIndex(b => b.StartedAt).HasDatabaseName("IX_CatalogImportBatches_StartedAt");
    }
}
