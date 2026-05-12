using McBrokers.Domain.Insurers.AxaDxn;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McBrokers.Infrastructure.Persistence.Configurations;

public class AxaDxnBusinessConfiguration : IEntityTypeConfiguration<AxaDxnBusiness>
{
    public void Configure(EntityTypeBuilder<AxaDxnBusiness> builder)
    {
        builder.ToTable("AxaDxnBusinesses");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.AxaDxnConfigId).IsRequired();

        builder.Property(b => b.Nombre)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(b => b.PolizaAutos).HasMaxLength(50);
        builder.Property(b => b.PolizaPickup).HasMaxLength(50);
        builder.Property(b => b.Mes).IsRequired();

        builder.HasIndex(b => new { b.AxaDxnConfigId, b.Nombre })
            .IsUnique()
            .HasDatabaseName("UX_AxaDxnBusinesses_Config_Nombre");

        builder.HasOne<AxaDxnConfig>()
            .WithMany()
            .HasForeignKey(b => b.AxaDxnConfigId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
