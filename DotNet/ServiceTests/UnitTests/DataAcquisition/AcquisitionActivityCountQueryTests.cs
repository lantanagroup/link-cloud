using FluentAssertions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition;

[Trait("Category", "UnitTests")]
public class AcquisitionActivityCountQueryTests : IDisposable
{
    private static readonly DateTime UtcNow = new(2026, 10, 7, 15, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public AcquisitionActivityCountQueryTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Failed_total_is_unbounded_and_the_series_is_utc_days()
    {
        await using var context = CreateContext();
        context.Logs.AddRange(
            Log(RequestStatus.Failed, new DateTime(2026, 10, 7, 1, 0, 0, DateTimeKind.Utc)),
            Log(RequestStatus.MaxRetriesReached, new DateTime(2026, 10, 6, 1, 0, 0, DateTimeKind.Utc)),
            Log(RequestStatus.Completed, new DateTime(2026, 10, 7, 2, 0, 0, DateTimeKind.Utc)),
            Log(RequestStatus.Failed, execution: null),
            Log(RequestStatus.Failed, new DateTime(2026, 10, 7, 3, 0, 0, DateTimeKind.Utc), deleted: true),
            Log(RequestStatus.Failed, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)));
        await context.SaveChangesAsync();

        var counts = await AcquisitionActivityCountQuery.ExecuteAsync(
            context.Logs.AsNoTracking(), 7, UtcNow, CancellationToken.None);

        counts.FailedTotal.Should().Be(4);
        counts.Days.Should().HaveCount(7);
        counts.Days.Select(day => day.Day).Should().Equal(
            "2026-10-01", "2026-10-02", "2026-10-03", "2026-10-04", "2026-10-05", "2026-10-06", "2026-10-07");
        counts.Days.Single(day => day.Day == "2026-10-07").Total.Should().Be(2);
        counts.Days.Single(day => day.Day == "2026-10-07").Failed.Should().Be(1);
        counts.Days.Single(day => day.Day == "2026-10-06").Failed.Should().Be(1);
        counts.Days.Single(day => day.Day == "2026-10-01").Total.Should().Be(0);
    }

    [Fact]
    public async Task Thousands_of_logs_return_counts_only()
    {
        await using var context = CreateContext();
        var when = new DateTime(2026, 10, 7, 4, 0, 0, DateTimeKind.Utc);
        context.Logs.AddRange(Enumerable.Range(0, 2000).Select(index =>
            Log(index % 5 == 0 ? RequestStatus.Failed : RequestStatus.Completed, when, facility: $"f-{index}")));
        await context.SaveChangesAsync();

        var counts = await AcquisitionActivityCountQuery.ExecuteAsync(
            context.Logs.AsNoTracking(), 7, UtcNow, CancellationToken.None);

        counts.FailedTotal.Should().Be(400);
        counts.Days.Single(day => day.Day == "2026-10-07").Total.Should().Be(2000);
        counts.Days.Single(day => day.Day == "2026-10-07").Failed.Should().Be(400);
    }

    private AcquisitionCountContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AcquisitionCountContext>().UseSqlite(_connection).Options;
        var context = new AcquisitionCountContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static DataAcquisitionLog Log(
        RequestStatus status,
        DateTime? execution,
        bool deleted = false,
        string facility = "facility-1") => new()
    {
        FacilityId = facility,
        Priority = AcquisitionPriority.Normal,
        Status = status,
        ExecutionDate = execution,
        IsDeleted = deleted
    };

    private sealed class AcquisitionCountContext : DbContext
    {
        public AcquisitionCountContext(DbContextOptions<AcquisitionCountContext> options) : base(options)
        {
        }

        public DbSet<DataAcquisitionLog> Logs => Set<DataAcquisitionLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<DataAcquisitionLog>();
            entity.Ignore(log => log.ScheduledReportEntity);
            entity.Ignore(log => log.FhirQueries);
            entity.Ignore(log => log.NoteEntries);
            entity.Ignore(log => log.ResourceIds);
            entity.Ignore(log => log.ReferenceResources);
        }
    }
}
