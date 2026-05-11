using McBrokers.Domain.Insurers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McBrokers.Infrastructure.Persistence.Configurations;

public class InsurerConfiguration : IEntityTypeConfiguration<Insurer>
{
    public void Configure(EntityTypeBuilder<Insurer> builder)
    {
        builder.ToTable("Insurers");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.Code)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.IsEnabled).IsRequired();
        builder.Property(i => i.DisplayOrder).IsRequired();
        builder.Property(i => i.LogoUrl).HasMaxLength(2000);

        builder.HasIndex(i => i.Code).IsUnique().HasDatabaseName("UX_Insurers_Code");
    }
}
