using Microsoft.EntityFrameworkCore;
using ReleasePilot.Domain.Aggregates;
using ReleasePilot.Infrastructure.Persistence.Configurations;

namespace ReleasePilot.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<StateTransitionEntity> StateTransitions => Set<StateTransitionEntity>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new PromotionConfiguration());
        modelBuilder.ApplyConfiguration(new StateTransitionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogEntryConfiguration());
    }
}

public class StateTransitionEntity
{
    public Guid Id { get; set; }
    public Guid PromotionId { get; set; }
    public string FromStatus { get; set; } = default!;
    public string ToStatus { get; set; } = default!;
    public string ActingUser { get; set; } = default!;
    public DateTime TransitionedAt { get; set; }
    public string? Reason { get; set; }
}

public class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = default!;
    public string Payload { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public bool IsProcessed { get; set; }
}

public class AuditLogEntry
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = default!;
    public Guid PromotionId { get; set; }
    public string ActingUser { get; set; } = default!;
    public DateTime Timestamp { get; set; }
    public string Payload { get; set; } = default!;
}
