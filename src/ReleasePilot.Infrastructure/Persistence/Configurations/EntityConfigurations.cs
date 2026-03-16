using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReleasePilot.Domain.Aggregates;

namespace ReleasePilot.Infrastructure.Persistence.Configurations;

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("Promotions");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ApplicationName).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Version).IsRequired().HasMaxLength(100);
        builder.Property(p => p.SourceEnvironment).HasConversion<string>().HasMaxLength(50);
        builder.Property(p => p.TargetEnvironment).HasConversion<string>().HasMaxLength(50);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(p => p.RequestedBy).IsRequired().HasMaxLength(200);
        builder.Property(p => p.RollbackReason).HasMaxLength(2000);

        builder.Property(p => p.WorkItemReferences)
            .HasConversion(
                v => string.Join(",", v),
                v => v.Split(",", StringSplitOptions.RemoveEmptyEntries).ToList());

        // Ignore domain events (transient, not persisted)
        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.StateHistory);

        builder.HasIndex(p => new { p.ApplicationName, p.TargetEnvironment, p.Status });
        builder.HasIndex(p => new { p.ApplicationName, p.CreatedAt });
    }
}

public class StateTransitionEntityConfiguration : IEntityTypeConfiguration<StateTransitionEntity>
{
    public void Configure(EntityTypeBuilder<StateTransitionEntity> builder)
    {
        builder.ToTable("StateTransitions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.FromStatus).IsRequired().HasMaxLength(50);
        builder.Property(s => s.ToStatus).IsRequired().HasMaxLength(50);
        builder.Property(s => s.ActingUser).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Reason).HasMaxLength(2000);

        builder.HasIndex(s => s.PromotionId);
    }
}

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.EventType).IsRequired().HasMaxLength(200);
        builder.Property(o => o.Payload).IsRequired();

        builder.HasIndex(o => o.IsProcessed);
    }
}

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLog");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.EventType).IsRequired().HasMaxLength(200);
        builder.Property(a => a.ActingUser).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Payload).IsRequired();

        builder.HasIndex(a => a.PromotionId);
        builder.HasIndex(a => a.Timestamp);
    }
}
