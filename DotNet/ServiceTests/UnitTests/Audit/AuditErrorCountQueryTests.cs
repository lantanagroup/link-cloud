using FluentAssertions;
using LantanaGroup.Link.Audit.Application.Queries;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Audit;

[Trait("Category", "UnitTests")]
public class AuditErrorCountQueryTests : IDisposable
{
    private static readonly DateTime UtcNow = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public AuditErrorCountQueryTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Errors_are_notes_containing_fail_inside_the_created_on_window()
    {
        await using var context = CreateContext();
        context.Logs.AddRange(
            Row("Failed to upload the package", UtcNow.AddHours(-2)),
            Row("processing failure while saving", UtcNow.AddHours(-20)),
            Row("Created schedule", UtcNow.AddHours(-1)),
            Row("Failed earlier", UtcNow.AddHours(-30)),
            Row(null, UtcNow.AddHours(-1)),
            Row("FAILURE in uppercase", UtcNow.AddMinutes(-5)));
        await context.SaveChangesAsync();

        var counts = await AuditErrorCountQuery.ExecuteAsync(context.Logs.AsNoTracking(), 24, UtcNow, CancellationToken.None);

        counts.Hours.Should().Be(24);
        counts.Errors.Should().Be(3);
        counts.WindowStartUtc.Should().Be(UtcNow.AddHours(-24));
        counts.WindowEndUtc.Should().Be(UtcNow);
    }

    [Fact]
    public void Hours_are_bounded()
    {
        AggregateCountLimits.TryHours(0, out _).Should().BeFalse();
        AggregateCountLimits.TryHours(169, out _).Should().BeFalse();
        AggregateCountLimits.TryHours(168, out _).Should().BeTrue();
        AggregateCountLimits.DefaultAuditHours.Should().Be(24);
    }

    private AuditCountContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AuditCountContext>().UseSqlite(_connection).Options;
        var context = new AuditCountContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static AuditLog Row(string? notes, DateTime createdOn) => new()
    {
        AuditId = AuditId.NewId(),
        Notes = notes,
        CreatedOn = createdOn,
        Action = "Submit"
    };

    private sealed class AuditCountContext : DbContext
    {
        public AuditCountContext(DbContextOptions<AuditCountContext> options) : base(options)
        {
        }

        public DbSet<AuditLog> Logs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<AuditLog>();
            entity.HasKey(log => log.Id);
            entity.Property(log => log.AuditId).HasConversion(id => id.Value, value => new AuditId(value));
            entity.Ignore(log => log.PropertyChanges);
        }
    }
}
