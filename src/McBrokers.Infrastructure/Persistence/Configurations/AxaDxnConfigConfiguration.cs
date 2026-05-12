using McBrokers.Domain.Insurers;
using McBrokers.Domain.Insurers.AxaDxn;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McBrokers.Infrastructure.Persistence.Configurations;

public class AxaDxnConfigConfiguration : IEntityTypeConfiguration<AxaDxnConfig>
{
    public void Configure(EntityTypeBuilder<AxaDxnConfig> builder)
    {
        builder.ToTable("AxaDxnConfigs");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.InsurerId).IsRequired();

        builder.Property(c => c.Usuario).HasMaxLength(100).IsRequired();
        // Password se persiste como ciphertext por IPasswordProtector en el repositorio.
        // Aquí solo declaramos la columna; capacidad amplia porque DataProtection genera ~150 chars
        // por cada string de 8 chars y crece con el tamaño.
        builder.Property(c => c.Password).HasMaxLength(2000).IsRequired();
        builder.Property(c => c.Tarifa).HasMaxLength(100).IsRequired();
        builder.Property(c => c.TarifaPickup).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Descuento).IsRequired();
        builder.Property(c => c.DescuentoPickup).IsRequired();
        builder.Property(c => c.MesPolizaDefault).IsRequired();

        // CopsisD4Key y CopsisB también se cifran (DataProtection) — mismo trato que Password.
        // El tamaño 2000 cubre el ciphertext expandido para secrets de hasta ~200 chars plaintext.
        builder.Property(c => c.CopsisD4Key).HasMaxLength(2000).IsRequired();
        builder.Property(c => c.CopsisB).HasMaxLength(2000).IsRequired();

        builder.HasIndex(c => c.InsurerId)
            .IsUnique()
            .HasDatabaseName("UX_AxaDxnConfigs_Insurer");

        builder.HasOne<Insurer>()
            .WithMany()
            .HasForeignKey(c => c.InsurerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
