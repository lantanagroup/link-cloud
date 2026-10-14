using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Operations.Persistence;

public sealed class OperationsDbContext : DbContext
{
    public OperationsDbContext(DbContextOptions<OperationsDbContext> options) : base(options) { }

    public DbSet<ChangeRequestEntity> ChangeRequests => Set<ChangeRequestEntity>();
    public DbSet<ExecutorLeaseEntity> ExecutorLeases => Set<ExecutorLeaseEntity>();
    public DbSet<MigrationEntity> Migrations => Set<MigrationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChangeRequestEntity>(entity =>
        {
            entity.ToTable("KafkaChangeRequests");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.Environment).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Kind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Topic).HasMaxLength(249).IsRequired();
            entity.Property(item => item.Payload).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => new { item.Environment, item.IsOpen });
            entity.HasIndex(item => new { item.Environment, item.CreatedUnixMs });
        });

        modelBuilder.Entity<ExecutorLeaseEntity>(entity =>
        {
            entity.ToTable("ExecutorLeases");
            entity.HasKey(item => item.Name);
            entity.Property(item => item.Name).HasMaxLength(128);
            entity.Property(item => item.Owner).HasMaxLength(200);
        });

        modelBuilder.Entity<MigrationEntity>(entity =>
        {
            entity.ToTable("KafkaTopicMigrations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.Environment).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Topic).HasMaxLength(249).IsRequired();
            entity.Property(item => item.Payload).IsRequired();
            entity.HasIndex(item => item.Environment);
        });
    }
}

/// <summary>One Kafka change request. The record itself is JSON in Payload; the columns are for queries and the version check.</summary>
public sealed class ChangeRequestEntity
{
    public Guid Id { get; set; }
    public string Environment { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Status { get; set; } = "";
    public string Topic { get; set; } = "";
    public bool IsOpen { get; set; }
    public long CreatedUnixMs { get; set; }
    public long UpdatedUnixMs { get; set; }
    public long Version { get; set; }
    public string Payload { get; set; } = "";
}

/// <summary>The single-executor lease. Fence goes up on every acquire so a stale holder's writes are refused.</summary>
public sealed class ExecutorLeaseEntity
{
    public string Name { get; set; } = "";
    public string? Owner { get; set; }
    public long ExpiresUnixMs { get; set; }
    public long Fence { get; set; }
}

public sealed class MigrationEntity
{
    public Guid Id { get; set; }
    public string Environment { get; set; } = "";
    public string Topic { get; set; } = "";
    public long Fence { get; set; }
    public long UpdatedUnixMs { get; set; }
    public string Payload { get; set; } = "";
}
