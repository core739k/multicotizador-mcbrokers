using McBrokers.Domain.Quotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McBrokers.Infrastructure.Persistence.Configurations;

public class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
{
    public void Configure(EntityTypeBuilder<Quotation> builder)
    {
        builder.ToTable("Quotations");

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();

        builder.Property(q => q.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(q => q.AgentId).IsRequired();
        builder.Property(q => q.VehicleMasterId).IsRequired();
        builder.Property(q => q.Package).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(q => q.PaymentMode).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(q => q.ValuationType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(q => q.SumInsured).HasPrecision(18, 2).IsRequired();
        builder.Property(q => q.PostalCode).HasMaxLength(5).IsRequired();
        builder.Property(q => q.CustomerSnapshotJson).IsRequired();
        builder.Property(q => q.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(q => q.ExpectedResultsCount).IsRequired();
        builder.Property(q => q.CreatedAt).IsRequired();

        builder.Ignore(q => q.Results);

        builder.HasIndex(q => q.CorrelationId).HasDatabaseName("IX_Quotations_CorrelationId");
        builder.HasIndex(q => q.CreatedAt).HasDatabaseName("IX_Quotations_CreatedAt");
        builder.HasIndex(q => q.AgentId).HasDatabaseName("IX_Quotations_AgentId");
    }
}

public class QuotationInsurerResultConfiguration : IEntityTypeConfiguration<QuotationInsurerResult>
{
    public void Configure(EntityTypeBuilder<QuotationInsurerResult> builder)
    {
        builder.ToTable("QuotationInsurerResults");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.QuotationId).IsRequired();
        builder.Property(r => r.InsurerId).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.ErrorCategory).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.ErrorCode).HasMaxLength(50);
        builder.Property(r => r.ErrorMessageHuman).HasMaxLength(1000);
        builder.Property(r => r.PremiumTotal).HasPrecision(18, 2);
        builder.Property(r => r.PremiumNet).HasPrecision(18, 2);
        builder.Property(r => r.Tax).HasPrecision(18, 2);
        builder.Property(r => r.Fees).HasPrecision(18, 2);
        builder.Property(r => r.LatencyMs).IsRequired();
        builder.Property(r => r.ExternalQuoteRef).HasMaxLength(100);
        builder.Property(r => r.RequestBlobRef).HasMaxLength(500);
        builder.Property(r => r.ResponseBlobRef).HasMaxLength(500);
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.Property(r => r.Version).IsRequired().HasDefaultValue(1);
        builder.Property(r => r.IsCurrent).IsRequired().HasDefaultValue(true);

        // Overrides como columnas planas (owned entity). 5 nullables sobre el
        // hot path es aceptable; facilita queries y reportes sin tener que
        // deserializar JSON.
        builder.OwnsOne(r => r.Overrides, o =>
        {
            o.Property(x => x.VehicleMasterId).HasColumnName("Override_VehicleMasterId");
            o.Property(x => x.Valuation).HasConversion<string>().HasMaxLength(30).HasColumnName("Override_Valuation");
            o.Property(x => x.MaterialDamagesDeductiblePct).HasPrecision(5, 2).HasColumnName("Override_DMPct");
            o.Property(x => x.RobberyDeductiblePct).HasPrecision(5, 2).HasColumnName("Override_RTPct");
            o.Property(x => x.MedicalExpensesSumInsured).HasPrecision(18, 2).HasColumnName("Override_GMOSumInsured");
        });

        builder.HasIndex(r => r.QuotationId).HasDatabaseName("IX_QuotationInsurerResults_QuotationId");
        // Único solo para resultados vigentes — permite versiones múltiples por
        // (QuotationId, InsurerId) mientras solo una tenga IsCurrent=true.
        builder.HasIndex(r => new { r.QuotationId, r.InsurerId })
            .IsUnique()
            .HasFilter("[IsCurrent] = 1")
            .HasDatabaseName("UX_QuotationInsurerResults_Quotation_Insurer_Current");
    }
}

public class KnownInsurerErrorConfiguration : IEntityTypeConfiguration<KnownInsurerError>
{
    public void Configure(EntityTypeBuilder<KnownInsurerError> builder)
    {
        builder.ToTable("KnownInsurerErrors");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.InsurerId).IsRequired();
        builder.Property(e => e.ExternalCode).HasMaxLength(50).IsRequired();
        builder.Property(e => e.ExternalMessagePattern).HasMaxLength(500);
        builder.Property(e => e.Category).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.HumanMessage).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.SuggestedAction).HasMaxLength(1000);
        builder.Property(e => e.AutoRetry).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasIndex(e => new { e.InsurerId, e.ExternalCode })
            .IsUnique()
            .HasDatabaseName("UX_KnownInsurerErrors_Insurer_Code");
    }
}
