using McBrokers.Domain.Insurers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McBrokers.Infrastructure.Persistence.Configurations;

public class InsurerPackageMappingConfiguration : IEntityTypeConfiguration<InsurerPackageMapping>
{
    public void Configure(EntityTypeBuilder<InsurerPackageMapping> builder)
    {
        builder.ToTable("InsurerPackageMappings");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.InsurerId).IsRequired();
        builder.Property(m => m.InternalPackage).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(m => m.ExternalCode).HasMaxLength(50).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(200);

        builder.HasIndex(m => new { m.InsurerId, m.InternalPackage })
            .IsUnique()
            .HasDatabaseName("UX_InsurerPackageMappings_Insurer_Package");

        builder.HasOne<Insurer>()
            .WithMany()
            .HasForeignKey(m => m.InsurerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
