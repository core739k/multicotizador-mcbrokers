using McBrokers.Domain.Insurers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McBrokers.Infrastructure.Persistence.Configurations;

public class InsurerConfigConfiguration : IEntityTypeConfiguration<InsurerConfig>
{
    public void Configure(EntityTypeBuilder<InsurerConfig> builder)
    {
        builder.ToTable("InsurerConfigs");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.InsurerId).IsRequired();

        builder.Property(c => c.EndpointUrl).HasMaxLength(2000).IsRequired();
        builder.Property(c => c.BusinessNumber).HasMaxLength(50).IsRequired();
        builder.Property(c => c.AgentCode).HasMaxLength(50).IsRequired();
        builder.Property(c => c.KeyVaultSecretName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.TimeoutSeconds).IsRequired();
        builder.Property(c => c.MaxRetries).IsRequired();

        builder.HasIndex(c => c.InsurerId)
            .IsUnique()
            .HasDatabaseName("UX_InsurerConfigs_Insurer");

        builder.HasOne<Insurer>()
            .WithMany()
            .HasForeignKey(c => c.InsurerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
