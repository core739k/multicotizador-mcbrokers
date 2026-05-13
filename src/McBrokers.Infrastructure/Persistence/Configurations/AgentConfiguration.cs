using McBrokers.Domain.Agents;
using McBrokers.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McBrokers.Infrastructure.Persistence.Configurations;

public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("Agents");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedNever();

        builder.Property(a => a.Email)
            .HasConversion(
                email => email.Value,
                value => AgentEmail.Create(value).Value)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(a => a.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.IsActive)
            .IsRequired();

        builder.Property(a => a.IsTechnical)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.AgentCode)
            .HasMaxLength(Agent.AgentCodeMaxLength)
            .IsRequired(false);

        builder.HasIndex(a => a.Email)
            .IsUnique()
            .HasDatabaseName("UX_Agents_Email");

        // Único cuando hay valor; varios NULL permitidos en SQL Server vía filtered index.
        builder.HasIndex(a => a.AgentCode)
            .IsUnique()
            .HasFilter("[AgentCode] IS NOT NULL")
            .HasDatabaseName("UX_Agents_AgentCode");
    }
}
