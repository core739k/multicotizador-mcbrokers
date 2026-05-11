using McBrokers.Domain.Emissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McBrokers.Infrastructure.Persistence.Configurations;

public class EmissionConfiguration : IEntityTypeConfiguration<Emission>
{
    public void Configure(EntityTypeBuilder<Emission> builder)
    {
        builder.ToTable("Emissions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.QuotationInsurerResultId).IsRequired();
        builder.Property(e => e.AgentId).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.PolicyNumber).HasMaxLength(50);
        builder.Property(e => e.PdfBlobRef).HasMaxLength(500);
        builder.Property(e => e.FailureReason).HasMaxLength(1000);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.IssuedAt);

        builder.HasIndex(e => e.QuotationInsurerResultId)
            .IsUnique()
            .HasDatabaseName("UX_Emissions_QuotationInsurerResultId");
        builder.HasIndex(e => e.PolicyNumber).HasDatabaseName("IX_Emissions_PolicyNumber");
    }
}

public class EmissionAttemptConfiguration : IEntityTypeConfiguration<EmissionAttempt>
{
    public void Configure(EntityTypeBuilder<EmissionAttempt> builder)
    {
        builder.ToTable("EmissionAttempts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.EmissionId).IsRequired();
        builder.Property(a => a.AttemptNumber).IsRequired();
        builder.Property(a => a.Outcome).HasMaxLength(50).IsRequired();
        builder.Property(a => a.LatencyMs).IsRequired();
        builder.Property(a => a.ErrorCode).HasMaxLength(50);
        builder.Property(a => a.CreatedAt).IsRequired();

        builder.HasIndex(a => a.EmissionId).HasDatabaseName("IX_EmissionAttempts_EmissionId");
        builder.HasOne<Emission>().WithMany().HasForeignKey(a => a.EmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}
