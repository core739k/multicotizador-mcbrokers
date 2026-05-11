using McBrokers.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McBrokers.Infrastructure.Persistence.Configurations;

public class VehicleMasterConfiguration : IEntityTypeConfiguration<VehicleMaster>
{
    public void Configure(EntityTypeBuilder<VehicleMaster> builder)
    {
        builder.ToTable("VehicleMasters");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.Year).IsRequired();
        builder.Property(v => v.Brand).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Model).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Version).HasMaxLength(200).IsRequired();
        builder.Property(v => v.BodyType).HasMaxLength(50);
        builder.Property(v => v.Transmission).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(v => v.Doors).IsRequired();
        builder.Property(v => v.Cylinders).IsRequired();
        builder.Property(v => v.IsActive).IsRequired();

        builder.HasIndex(v => new { v.Year, v.Brand })
            .HasDatabaseName("IX_VehicleMasters_Year_Brand");

        builder.HasIndex(v => new { v.Year, v.Brand, v.Model, v.Version })
            .HasDatabaseName("IX_VehicleMasters_Year_Brand_Model_Version");
    }
}
