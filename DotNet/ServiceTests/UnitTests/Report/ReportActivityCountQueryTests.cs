using FluentAssertions;
using LantanaGroup.Link.Report.Business.Queries;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

/// <summary>
/// Schedule aggregates run in the database. The slim context maps only the schedule row
/// so SQLite does not have to build the report graph or the descending index.
/// </summary>
[Trait("Category", "UnitTests")]
public class ReportActivityCountQueryTests : IDisposable
{
    private static readonly DateTime UtcNow = new(2026, 10, 7, 15, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public ReportActivityCountQueryTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Counts_statuses_excludes_deleted_and_fills_utc_days()
    {
        await using var context = CreateContext();
        context.Schedules.AddRange(
            Row("a", ScheduleStatus.New, new DateTime(2026, 10, 7, 1, 0, 0, DateTimeKind.Utc)),
            Row("a", ScheduleStatus.Scheduled, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)),
            Row("b", ScheduleStatus.EndOfPeriod, new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc)),
            Row("a", ScheduleStatus.Submitted, new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc)),
            Row("b", ScheduleStatus.CompletedNotSubmitted, new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc)),
            Row("a", ScheduleStatus.Submitted, new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc), deleted: true),
            Row("c", ScheduleStatus.New, new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc), deleted: null));
        await context.SaveChangesAsync();

        var counts = await ReportActivityCountQuery.ExecuteAsync(
            ReportActivityCountQuery.Apply(context.Schedules.AsNoTracking(), null, null),
            days: 7,
            UtcNow,
            CancellationToken.None);

        counts.InFlight.Should().Be(4);
        counts.Submitted.Should().Be(1);
        counts.NotSubmitted.Should().Be(1);
        counts.Failed.Should().Be(0);
        counts.OldestInFlightUtc.Should().Be(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        counts.CreatedPerDay.Select(day => day.Day).Should().Equal(
            "2026-10-01", "2026-10-02", "2026-10-03", "2026-10-04", "2026-10-05", "2026-10-06", "2026-10-07");
        counts.CreatedPerDay.Single(day => day.Day == "2026-10-01").Count.Should().Be(0);
        counts.CreatedPerDay.Single(day => day.Day == "2026-10-07").Count.Should().Be(1);
        counts.CreatedPerDay.Single(day => day.Day == "2026-10-05").Count.Should().Be(0);
        counts.CreatedPerDay.Sum(day => day.Count).Should().Be(5);
    }

    [Fact]
    public async Task Include_and_exclude_filter_in_the_database()
    {
        await using var context = CreateContext();
        context.Schedules.AddRange(
            Row("a", ScheduleStatus.Submitted, new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc)),
            Row("b", ScheduleStatus.New, new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc)),
            Row("c", ScheduleStatus.Scheduled, new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc)));
        await context.SaveChangesAsync();

        var included = await ReportActivityCountQuery.ExecuteAsync(
            ReportActivityCountQuery.Apply(context.Schedules.AsNoTracking(), ["a", "c"], null),
            7, UtcNow, CancellationToken.None);
        included.Submitted.Should().Be(1);
        included.InFlight.Should().Be(1);

        var excluded = await ReportActivityCountQuery.ExecuteAsync(
            ReportActivityCountQuery.Apply(context.Schedules.AsNoTracking(), null, ["a"]),
            7, UtcNow, CancellationToken.None);
        excluded.Submitted.Should().Be(0);
        excluded.InFlight.Should().Be(2);
    }

    [Fact]
    public async Task Thousands_of_rows_return_counts_only()
    {
        await using var context = CreateContext();
        var created = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        var rows = Enumerable.Range(0, 2000).Select(index =>
            Row($"facility-{index}", ScheduleStatus.New, created)).ToList();
        context.Schedules.AddRange(rows);
        await context.SaveChangesAsync();

        var counts = await ReportActivityCountQuery.ExecuteAsync(
            ReportActivityCountQuery.Apply(context.Schedules.AsNoTracking(), null, null),
            7, UtcNow, CancellationToken.None);

        counts.InFlight.Should().Be(2000);
        counts.CreatedPerDay.Should().HaveCount(7);
        counts.CreatedPerDay.Sum(day => day.Count).Should().Be(2000);
        counts.CreatedPerDay.Should().OnlyContain(day => day.Day.Length == 10);
    }

    [Fact]
    public void Days_and_facility_ids_are_bounded()
    {
        AggregateCountLimits.TryDays(0, out var low).Should().BeFalse();
        AggregateCountLimits.TryDays(32, out var high).Should().BeFalse();
        AggregateCountLimits.TryDays(31, out _).Should().BeTrue();

        var tooMany = Enumerable.Range(0, 5001).Select(index => $"id-{index}").ToList();
        AggregateCountLimits.TryFacilityIds(tooMany, out _, out var sizeError).Should().BeFalse();
        sizeError.Should().NotBeNullOrEmpty();

        AggregateCountLimits.TryFacilityIds(["  a  ", "a", ""], out var normalized, out _).Should().BeTrue();
        normalized.Should().Equal("a");

        AggregateCountLimits.TryFacilityIds(["bad<id"], out _, out var invalid).Should().BeFalse();
        invalid.Should().NotBeNullOrEmpty();

        AggregateCountLimits.TryFacilityIds([], out var empty, out _).Should().BeTrue();
        empty.Should().NotBeNull().And.BeEmpty();

        AggregateCountLimits.TryFacilityIds(null, out var absent, out _).Should().BeTrue();
        absent.Should().BeNull();
    }

    private ReportCountContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ReportCountContext>().UseSqlite(_connection).Options;
        var context = new ReportCountContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static ReportSchedule Row(string facility, ScheduleStatus status, DateTime created, bool? deleted = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            FacilityId = facility,
            Status = status,
            CreateDate = created,
            IsDeleted = deleted,
            ReportStartDate = new DateTimeOffset(created),
            ReportEndDate = new DateTimeOffset(created),
            Frequency = Frequency.Daily
        };

    private sealed class ReportCountContext : DbContext
    {
        public ReportCountContext(DbContextOptions<ReportCountContext> options) : base(options)
        {
        }

        public DbSet<ReportSchedule> Schedules => Set<ReportSchedule>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<ReportSchedule>();
            entity.Ignore(schedule => schedule.ReportEntries);
            entity.Ignore(schedule => schedule.ReportPopulations);
            entity.Ignore(schedule => schedule.ReportResources);
            entity.Ignore(schedule => schedule.ReportTypes);
            foreach (var index in entity.Metadata.GetIndexes().ToList())
            {
                entity.Metadata.RemoveIndex(index.Properties);
            }
        }
    }
}
