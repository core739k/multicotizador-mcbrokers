using McBrokers.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McBrokers.Infrastructure.Persistence.Configurations;

public class VehicleInsurerMappingConfiguration : IEntityTypeConfiguration<VehicleInsurerMapping>
{
    public void Configure(EntityTypeBuilder<VehicleInsurerMapping> builder)
    {
        builder.ToTable("VehicleInsurerMappings");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.VehicleMasterId).IsRequired();
        builder.Property(m => m.InsurerId).IsRequired();
        builder.Property(m => m.ExternalClave).HasMaxLength(50).IsRequired();
        builder.Property(m => m.InsurerBrandRaw).HasMaxLength(200);
        builder.Property(m => m.InsurerModelRaw).HasMaxLength(200);
        builder.Property(m => m.InsurerVersionRaw).HasMaxLength(200);
        builder.Property(m => m.ConfidenceScore).HasPrecision(5, 2).IsRequired();
        builder.Property(m => m.ReviewState).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.ReviewedByAgentId);
        builder.Property(m => m.ReviewedAt);
        builder.Property(m => m.CreatedAt).IsRequired();

        // Covering index for O(1) AMIS lookup at quotation time.
        builder.HasIndex(m => new { m.VehicleMasterId, m.InsurerId })
            .HasDatabaseName("IX_VehicleInsurerMappings_Master_Insurer");

        builder.HasIndex(m => new { m.InsurerId, m.ExternalClave })
            .IsUnique()
            .HasDatabaseName("UX_VehicleInsurerMappings_Insurer_ExternalClave");

        builder.HasIndex(m => m.ReviewState)
            .HasDatabaseName("IX_VehicleInsurerMappings_ReviewState");

        builder.HasOne<VehicleMaster>()
            .WithMany()
            .HasForeignKey(m => m.VehicleMasterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
