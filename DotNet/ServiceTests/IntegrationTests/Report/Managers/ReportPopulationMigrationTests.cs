using System.Data;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report.Managers;

/// <summary>
/// Proves the unique-key migration against SQL Server LocalDB.
/// EnsureCreated would build the new indexes directly and skip the duplicate cleanup.
/// The test skips when MSSQLLocalDB is not installed (CI agents).
/// </summary>
[Collection("ReportPopulationLocalDb")]
public sealed class ReportPopulationMigrationTests
{
    private const string PreviousMigration = "20260827211554_AddReportEntryMappingOutcome";
    private const string ThisMigration = "20261008152257_UniqueReportPopulationKeys";

    [LocalDbFact]
    public async Task Migration_CollapsesDuplicates_AndLeavesUntouchedTotals()
    {
        await using var database = await LocalDbReportDatabase.CreateUnmigratedAsync();
        var scheduleId = Guid.NewGuid();
        var keeperPopulationId = Guid.NewGuid();

        await using (var scope = database.Provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
            await MigrateToAsync(context, PreviousMigration);

            context.ReportSchedule.Add(Schedule(scheduleId, "fac-merge"));
            await context.SaveChangesAsync();

            var keeper = Population(keeperPopulationId, scheduleId, "fac-merge", "dup-type", DateTime.UtcNow.AddHours(-2));
            keeper.GroupPopulations.Add(Group("initial-population", 50, Child("mr-1", 3), Child("mr-2", 4)));
            keeper.GroupPopulations.Add(Group("numerator", 1, Child("mr-9", 2)));
            keeper.GroupPopulations.Add(Group("numerator", 13, Child("mr-9", 7), Child("mr-10", 6)));
            context.ReportPopulation.Add(keeper);
            await context.SaveChangesAsync();

            var duplicate = Population(Guid.NewGuid(), scheduleId, "fac-merge", "dup-type", DateTime.UtcNow.AddHours(-1));
            duplicate.GroupPopulations.Add(Group("initial-population", 14, Child("mr-2", 9), Child("mr-3", 5)));
            duplicate.GroupPopulations.Add(Group("denominator", 77, Child("mr-moved", 6)));
            context.ReportPopulation.Add(duplicate);
            await context.SaveChangesAsync();

            var untouched = Population(Guid.NewGuid(), scheduleId, "fac-merge", "untouched", DateTime.UtcNow);
            untouched.GroupPopulations.Add(Group("initial-population", 99, Child("mr-keep", 1)));
            context.ReportPopulation.Add(untouched);
            await context.SaveChangesAsync();

            var nulls = Population(Guid.NewGuid(), scheduleId, "fac-merge", "null-type", DateTime.UtcNow);
            nulls.GroupPopulations.Add(Group(null, 40, Child("mr-n1", 1)));
            nulls.GroupPopulations.Add(Group(null, 10, Child("mr-n1", 8), Child("mr-n2", 2)));
            context.ReportPopulation.Add(nulls);
            await context.SaveChangesAsync();

            var caseKeeper = Population(Guid.NewGuid(), scheduleId, "fac-merge", "CaseType", DateTime.UtcNow.AddHours(-2));
            caseKeeper.GroupPopulations.Add(Group("initial-population", 100, Child("mr-c", 5)));
            context.ReportPopulation.Add(caseKeeper);
            await context.SaveChangesAsync();

            var caseDuplicate = Population(Guid.NewGuid(), scheduleId, "fac-merge", "casetype", DateTime.UtcNow.AddHours(-1));
            caseDuplicate.GroupPopulations.Add(Group("initial-population", 50, Child("mr-d", 7)));
            context.ReportPopulation.Add(caseDuplicate);
            await context.SaveChangesAsync();

            await MigrateToAsync(context, null);
        }

        await using var readScope = database.Provider.CreateAsyncScope();
        var read = readScope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var populations = await read.ReportPopulation
            .AsNoTracking()
            .Include(p => p.GroupPopulations)
            .ThenInclude(g => g.MeasureReportPopulations)
            .Where(p => p.ReportScheduleId == scheduleId)
            .ToListAsync();

        var merged = Assert.Single(populations, p => string.Equals(p.ReportType, "dup-type", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(keeperPopulationId, merged.Id);

        var initial = Assert.Single(merged.GroupPopulations, g => g.PopulationId == "initial-population");
        Assert.Equal(12, initial.TotalPopulationCount);
        Assert.Equal(new[] { ("mr-1", 3), ("mr-2", 4), ("mr-3", 5) }, Counts(initial));

        var numerator = Assert.Single(merged.GroupPopulations, g => g.PopulationId == "numerator");
        Assert.Equal(8, numerator.TotalPopulationCount);
        Assert.Equal(new[] { ("mr-10", 6), ("mr-9", 2) }, Counts(numerator));

        var moved = Assert.Single(merged.GroupPopulations, g => g.PopulationId == "denominator");
        Assert.Equal(6, moved.TotalPopulationCount);
        Assert.Equal(new[] { ("mr-moved", 6) }, Counts(moved));

        var untouchedRow = Assert.Single(populations, p => p.ReportType == "untouched");
        var untouchedGroup = Assert.Single(untouchedRow.GroupPopulations);
        Assert.Equal(99, untouchedGroup.TotalPopulationCount);
        Assert.Equal(new[] { ("mr-keep", 1) }, Counts(untouchedGroup));

        var nullRow = Assert.Single(populations, p => p.ReportType == "null-type");
        var nullGroup = Assert.Single(nullRow.GroupPopulations);
        Assert.Null(nullGroup.PopulationId);
        Assert.Equal(3, nullGroup.TotalPopulationCount);
        Assert.Equal(new[] { ("mr-n1", 1), ("mr-n2", 2) }, Counts(nullGroup));

        var collation = await ReadCollationAsync(read);
        if (collation.Contains("_CI_", StringComparison.OrdinalIgnoreCase))
        {
            var caseRow = Assert.Single(populations, p => string.Equals(p.ReportType, "casetype", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("CaseType", caseRow.ReportType);
            var caseGroup = Assert.Single(caseRow.GroupPopulations);
            Assert.Equal(12, caseGroup.TotalPopulationCount);
            Assert.Equal(new[] { ("mr-c", 5), ("mr-d", 7) }, Counts(caseGroup));
        }
        else
        {
            Assert.Equal(2, populations.Count(p => p.ReportType is "CaseType" or "casetype"));
        }

        await AssertUniqueIndexesAsync(read);

        read.ReportPopulation.Add(Population(Guid.NewGuid(), scheduleId, "fac-merge", "dup-type", DateTime.UtcNow));
        var duplicateInsert = await Record.ExceptionAsync(() => read.SaveChangesAsync());
        Assert.IsType<DbUpdateException>(duplicateInsert);
    }

    [LocalDbFact]
    public async Task Migration_OnAnEmptyDatabase_CreatesUniqueIndexes_AndDownRestoresTheOldOnes()
    {
        await using var database = await LocalDbReportDatabase.CreateUnmigratedAsync();
        await using var scope = database.Provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

        await MigrateToAsync(context, null);
        await AssertUniqueIndexesAsync(context);
        Assert.Contains(ThisMigration, await context.Database.GetAppliedMigrationsAsync());

        await MigrateToAsync(context, PreviousMigration);
        await AssertOldIndexesAsync(context);
        Assert.DoesNotContain(ThisMigration, await context.Database.GetAppliedMigrationsAsync());

        await MigrateToAsync(context, null);
        await AssertUniqueIndexesAsync(context);
    }

    private static Task MigrateToAsync(ReportDbContext context, string? targetMigration)
    {
        var migrator = context.Database.GetService<IMigrator>()
            ?? throw new InvalidOperationException("Report migrations are not available.");
        return migrator.MigrateAsync(targetMigration);
    }

    private static ReportSchedule Schedule(Guid id, string facilityId)
    {
        return new ReportSchedule
        {
            Id = id,
            FacilityId = facilityId,
            CreateDate = DateTime.UtcNow,
            ReportStartDate = DateTimeOffset.UtcNow.AddDays(-7),
            ReportEndDate = DateTimeOffset.UtcNow.AddDays(7),
            EnableSubmission = true,
            Frequency = 0,
            Status = 0
        };
    }

    private static ReportPopulation Population(Guid id, Guid scheduleId, string facilityId, string reportType, DateTime createDate)
    {
        return new ReportPopulation
        {
            Id = id,
            FacilityId = facilityId,
            ReportScheduleId = scheduleId,
            ReportType = reportType,
            Measure = reportType,
            CreateDate = createDate
        };
    }

    private static GroupPopulation Group(string? populationId, int storedTotal, params MeasureReportPopulation[] children)
    {
        return new GroupPopulation
        {
            PopulationId = populationId,
            PopulationCodeJson = "{}",
            TotalPopulationCount = storedTotal,
            MeasureReportPopulations = children.ToList()
        };
    }

    private static MeasureReportPopulation Child(string measureReportId, int count)
    {
        return new MeasureReportPopulation
        {
            MeasureReportId = measureReportId,
            PopulationCount = count
        };
    }

    private static (string MeasureReportId, int PopulationCount)[] Counts(GroupPopulation group)
    {
        return group.MeasureReportPopulations
            .OrderBy(child => child.MeasureReportId, StringComparer.Ordinal)
            .Select(child => (child.MeasureReportId, child.PopulationCount))
            .ToArray();
    }

    private static async Task AssertUniqueIndexesAsync(ReportDbContext context)
    {
        var indexes = await ReadIndexesAsync(context);
        AssertIndex(indexes, "IX_ReportPopulation_Schedule_ReportType", unique: true, filtered: false);
        AssertIndex(indexes, "IX_GroupPopulation_Population_PopulationId", unique: true, filtered: false);
        AssertIndex(indexes, "IX_MeasureReportPopulation_Group_MeasureReport", unique: true, filtered: false);
        Assert.DoesNotContain(indexes, index => index.Name is
            "IX_ReportPopulation_ReportScheduleId" or
            "IX_GroupPopulation_ReportPopulationId" or
            "IX_MeasureReportPopulation_GroupPopulationId");
    }

    private static async Task AssertOldIndexesAsync(ReportDbContext context)
    {
        var indexes = await ReadIndexesAsync(context);
        AssertIndex(indexes, "IX_ReportPopulation_ReportScheduleId", unique: false, filtered: false);
        AssertIndex(indexes, "IX_GroupPopulation_ReportPopulationId", unique: false, filtered: false);
        AssertIndex(indexes, "IX_MeasureReportPopulation_GroupPopulationId", unique: false, filtered: false);
        Assert.DoesNotContain(indexes, index => index.Name is
            "IX_ReportPopulation_Schedule_ReportType" or
            "IX_GroupPopulation_Population_PopulationId" or
            "IX_MeasureReportPopulation_Group_MeasureReport");
    }

    private static void AssertIndex(IReadOnlyList<IndexRow> indexes, string name, bool unique, bool filtered)
    {
        var index = Assert.Single(indexes, row => row.Name == name);
        Assert.Equal(unique, index.Unique);
        Assert.Equal(filtered, !string.IsNullOrEmpty(index.Filter));
    }

    private static async Task<string> ReadCollationAsync(ReportDbContext context)
    {
        var result = await ExecuteScalarAsync(context, "SELECT CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS nvarchar(128));");
        return (string)result!;
    }

    private static async Task<IReadOnlyList<IndexRow>> ReadIndexesAsync(ReportDbContext context)
    {
        var rows = new List<IndexRow>();
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT i.name, i.is_unique, i.filter_definition
FROM sys.indexes AS i
INNER JOIN sys.tables AS t ON t.object_id = i.object_id
WHERE i.name IN (
    N'IX_ReportPopulation_Schedule_ReportType',
    N'IX_GroupPopulation_Population_PopulationId',
    N'IX_MeasureReportPopulation_Group_MeasureReport',
    N'IX_ReportPopulation_ReportScheduleId',
    N'IX_GroupPopulation_ReportPopulationId',
    N'IX_MeasureReportPopulation_GroupPopulationId')";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new IndexRow(
                reader.GetString(0),
                reader.GetBoolean(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return rows;
    }

    private static async Task<object?> ExecuteScalarAsync(ReportDbContext context, string sql)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private sealed record IndexRow(string Name, bool Unique, string? Filter);
}
