using McBrokers.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McBrokers.Infrastructure.Persistence.Configurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLog");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.AgentId);
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(200).IsRequired();
        builder.Property(a => a.CorrelationId).HasMaxLength(100);
        builder.Property(a => a.PayloadJson).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        builder.HasIndex(a => a.CreatedAt).HasDatabaseName("IX_AuditLog_CreatedAt");
        builder.HasIndex(a => new { a.EntityType, a.EntityId }).HasDatabaseName("IX_AuditLog_Entity");
        builder.HasIndex(a => a.CorrelationId).HasDatabaseName("IX_AuditLog_CorrelationId");
    }
}
