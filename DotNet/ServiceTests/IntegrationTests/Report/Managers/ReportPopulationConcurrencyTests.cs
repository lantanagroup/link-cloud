using System.Collections.Concurrent;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report.Managers;

/// <summary>
/// Proves population totals under real concurrency. EF InMemory cannot.
/// SQL Server LocalDB is used so the test can run without a container.
/// The test skips when MSSQLLocalDB is not installed (CI agents).
/// </summary>
[Collection("ReportPopulationLocalDb")]
public sealed class ReportPopulationConcurrencyTests
{
    private const int PatientCount = 8;

    [LocalDbFact]
    public async Task ConcurrentPatients_OnASharedRow_KeepTheSum_AndRedeliveryDoesNotDoubleCount()
    {
        await using var database = await LocalDbReportDatabase.CreateAsync();
        var scheduleId = Guid.NewGuid();
        const string facilityId = "fac-concurrency";
        const string reportType = "nhs-type";

        await database.SeedScheduleAndPopulationAsync(scheduleId, facilityId, reportType);

        var deliveries = BuildDeliveries(reportType, "wave");
        await ApplyAllAsync(database, facilityId, scheduleId, deliveries);

        await AssertPopulationAsync(database, scheduleId, reportType, expectedTotal: Sum(deliveries), expectedChildren: PatientCount);

        await ApplyAllAsync(database, facilityId, scheduleId, deliveries);

        await AssertPopulationAsync(database, scheduleId, reportType, expectedTotal: Sum(deliveries), expectedChildren: PatientCount);
    }

    [LocalDbFact]
    public async Task ConcurrentAdds_WhenTheRowIsMissing_CreateOneRowAndTheRightTotal()
    {
        await using var database = await LocalDbReportDatabase.CreateAsync();
        var scheduleId = Guid.NewGuid();
        const string facilityId = "fac-add-race";
        const string reportType = "add-type";

        await database.SeedScheduleAsync(scheduleId, facilityId);

        var deliveries = BuildDeliveries(reportType, "add");
        await ApplyAllAsync(database, facilityId, scheduleId, deliveries);

        await AssertPopulationAsync(database, scheduleId, reportType, expectedTotal: Sum(deliveries), expectedChildren: PatientCount);
    }

    [LocalDbFact]
    public async Task ConcurrentRedelivery_OfOneMeasureReport_CountsItOnce()
    {
        await using var database = await LocalDbReportDatabase.CreateAsync();
        var scheduleId = Guid.NewGuid();
        const string facilityId = "fac-once";
        const string reportType = "once-type";
        const string measureReportId = "mr-shared";
        const int count = 4;

        await database.SeedScheduleAndPopulationAsync(scheduleId, facilityId, reportType);

        var deliveries = Enumerable.Range(0, PatientCount)
            .Select(_ => Delivery(reportType, measureReportId, count))
            .ToArray();

        await ApplyAllAsync(database, facilityId, scheduleId, deliveries);

        await AssertPopulationAsync(database, scheduleId, reportType, expectedTotal: count, expectedChildren: 1);
    }

    [Fact]
    public async Task SequentialApply_OnSqlite_IncrementsOnceAndIgnoresRedelivery()
    {
        var path = Path.Combine(Path.GetTempPath(), $"report-pop-{Guid.NewGuid():N}.db");
        var scheduleId = Guid.NewGuid();
        const string facilityId = "fac-sqlite";
        const string reportType = "sqlite-type";

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<ReportDbContext>(options => options.UseSqlite($"Data Source={path};Pooling=False"));
        services.AddScoped<IReportPopulationManager, ReportPopulationManager>();

        await using (var provider = services.BuildServiceProvider())
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
                await context.Database.EnsureCreatedAsync();
                context.ReportSchedule.Add(new ReportSchedule
                {
                    Id = scheduleId,
                    FacilityId = facilityId,
                    CreateDate = DateTime.UtcNow,
                    ReportStartDate = DateTimeOffset.UtcNow.AddDays(-7),
                    ReportEndDate = DateTimeOffset.UtcNow.AddDays(7),
                    EnableSubmission = true,
                    Frequency = 0,
                    Status = 0
                });
                await context.SaveChangesAsync();
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var manager = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();
                var first = Delivery(reportType, "mr-1", 5);
                var again = Delivery(reportType, "mr-1", 5);
                var second = Delivery(reportType, "mr-2", 7);

                await manager.ApplyAggregateResultAsync(facilityId, scheduleId, first, CancellationToken.None);
                await manager.ApplyAggregateResultAsync(facilityId, scheduleId, again, CancellationToken.None);
                await manager.ApplyAggregateResultAsync(facilityId, scheduleId, second, CancellationToken.None);
            }

            await using var readScope = provider.CreateAsyncScope();
            var read = readScope.ServiceProvider.GetRequiredService<ReportDbContext>();
            var population = await read.ReportPopulation
                .AsNoTracking()
                .Include(p => p.GroupPopulations)
                .ThenInclude(g => g.MeasureReportPopulations)
                .SingleAsync(p => p.ReportScheduleId == scheduleId && p.ReportType == reportType);

            Assert.Equal(2, population.GroupPopulations.Count);
            foreach (var group in population.GroupPopulations)
            {
                Assert.Equal(12, group.TotalPopulationCount);
                Assert.Equal(2, group.MeasureReportPopulations.Count);
            }
        }

        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-journal", "-wal", "-shm" })
        {
            var file = path + suffix;
            if (File.Exists(file))
                File.Delete(file);
        }
    }

    private static AggregateMeasureReportResult[] BuildDeliveries(string reportType, string prefix)
    {
        return Enumerable.Range(1, PatientCount)
            .Select(n => Delivery(reportType, $"{prefix}-{n}", n))
            .ToArray();
    }

    private static AggregateMeasureReportResult Delivery(string reportType, string measureReportId, int count)
    {
        return new AggregateMeasureReportResult
        {
            Measure = "measure",
            ReportType = reportType,
            MeasureReportId = measureReportId,
            PopulationList = new List<AggregateMeasureReportPopulation>
            {
                new()
                {
                    PopulationId = "initial-population",
                    PopulationCount = count,
                    PopulationCode = new Hl7.Fhir.Model.CodeableConcept("http://example.com", "IP")
                },
                new()
                {
                    PopulationId = "denominator",
                    PopulationCount = count,
                    PopulationCode = new Hl7.Fhir.Model.CodeableConcept("http://example.com", "DENOM")
                }
            }
        };
    }

    private static int Sum(IEnumerable<AggregateMeasureReportResult> deliveries)
    {
        return deliveries.Sum(d => d.PopulationList[0].PopulationCount);
    }

    private static async Task ApplyAllAsync(
        LocalDbReportDatabase database,
        string facilityId,
        Guid scheduleId,
        IReadOnlyList<AggregateMeasureReportResult> deliveries)
    {
        var barrier = new Barrier(deliveries.Count);
        var errors = new ConcurrentQueue<Exception>();

        // Materialize first. SignalAndWait blocks the caller, so a lazy Select would
        // sit inside the first patient and never start the rest.
        var tasks = deliveries.Select(async delivery =>
        {
            await Task.Yield();
            var passedBarrier = false;
            try
            {
                await using var scope = database.Provider.CreateAsyncScope();
                var manager = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();

                // Hold every patient at the same point so the writes actually overlap.
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(30)))
                    throw new TimeoutException("Concurrent population apply did not start together.");

                passedBarrier = true;
                await manager.ApplyAggregateResultAsync(facilityId, scheduleId, delivery, CancellationToken.None);
            }
            catch (Exception ex)
            {
                if (!passedBarrier)
                {
                    try
                    {
                        barrier.RemoveParticipant();
                    }
                    catch (Exception signalEx)
                    {
                        errors.Enqueue(signalEx);
                    }
                }

                errors.Enqueue(ex);
            }
        });

        await Task.WhenAll(tasks.ToArray());

        if (!errors.IsEmpty)
        {
            throw new AggregateException(errors);
        }
    }

    private static async Task AssertPopulationAsync(
        LocalDbReportDatabase database,
        Guid scheduleId,
        string reportType,
        int expectedTotal,
        int expectedChildren)
    {
        await using var scope = database.Provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var populations = await context.ReportPopulation
            .AsNoTracking()
            .Include(p => p.GroupPopulations)
            .ThenInclude(g => g.MeasureReportPopulations)
            .Where(p => p.ReportScheduleId == scheduleId && p.ReportType == reportType)
            .ToListAsync();

        Assert.Single(populations);

        foreach (var populationId in new[] { "initial-population", "denominator" })
        {
            var groups = populations[0].GroupPopulations.Where(g => g.PopulationId == populationId).ToList();
            Assert.Single(groups);
            Assert.Equal(expectedTotal, groups[0].TotalPopulationCount);
            Assert.Equal(expectedChildren, groups[0].MeasureReportPopulations.Count);
            Assert.Equal(expectedChildren, groups[0].MeasureReportPopulations.Select(m => m.MeasureReportId).Distinct().Count());
        }
    }
}

public sealed class LocalDbFactAttribute : FactAttribute
{
    public LocalDbFactAttribute()
    {
        if (!LocalDbReportDatabase.IsAvailable)
        {
            Skip = "SQL Server LocalDB instance MSSQLLocalDB is not available. This concurrency test is skipped where LocalDB is not installed.";
        }
    }
}

public sealed class LocalDbReportDatabase : IAsyncDisposable
{
    private const string NamePrefix = "LEGLINK1442_";

    private readonly string _masterConnectionString;
    private readonly string _connectionString;
    private readonly string _databaseName;
    private readonly ServiceProvider _provider;

    private LocalDbReportDatabase(string masterConnectionString, string connectionString, string databaseName, ServiceProvider provider)
    {
        _masterConnectionString = masterConnectionString;
        _connectionString = connectionString;
        _databaseName = databaseName;
        _provider = provider;
    }

    public static bool IsAvailable { get; } = Probe();

    public ServiceProvider Provider => _provider;

    public static Task<LocalDbReportDatabase> CreateAsync()
    {
        return CreateCoreAsync(ensureCreated: true);
    }

    public static Task<LocalDbReportDatabase> CreateUnmigratedAsync()
    {
        return CreateCoreAsync(ensureCreated: false);
    }

    private static async Task<LocalDbReportDatabase> CreateCoreAsync(bool ensureCreated)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("LocalDB is not available.");
        }

        var master = MasterConnectionString();
        var databaseName = NamePrefix + Guid.NewGuid().ToString("N");
        await ExecuteAsync(master, $"CREATE DATABASE [{databaseName}];");
        await ExecuteAsync(master, $"ALTER DATABASE [{databaseName}] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;");

        var connectionString = new SqlConnectionStringBuilder(master)
        {
            InitialCatalog = databaseName,
            ConnectTimeout = 30
        }.ConnectionString;

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<ReportDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.CommandTimeout(180)));
        services.AddScoped<IReportPopulationManager, ReportPopulationManager>();
        var provider = services.BuildServiceProvider();

        if (ensureCreated)
        {
            await using var scope = provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
            await context.Database.EnsureCreatedAsync();
        }

        return new LocalDbReportDatabase(master, connectionString, databaseName, provider);
    }

    public async Task SeedScheduleAsync(Guid scheduleId, string facilityId)
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        context.ReportSchedule.Add(new ReportSchedule
        {
            Id = scheduleId,
            FacilityId = facilityId,
            CreateDate = DateTime.UtcNow,
            ReportStartDate = DateTimeOffset.UtcNow.AddDays(-7),
            ReportEndDate = DateTimeOffset.UtcNow.AddDays(7),
            EnableSubmission = true,
            Frequency = 0,
            Status = 0
        });
        await context.SaveChangesAsync();
    }

    public async Task SeedScheduleAndPopulationAsync(Guid scheduleId, string facilityId, string reportType)
    {
        await SeedScheduleAsync(scheduleId, facilityId);
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        context.ReportPopulation.Add(new ReportPopulation
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            ReportScheduleId = scheduleId,
            ReportType = reportType,
            Measure = reportType,
            CreateDate = DateTime.UtcNow,
            GroupPopulations = new List<GroupPopulation>
            {
                new()
                {
                    PopulationId = "initial-population",
                    TotalPopulationCount = 0,
                    PopulationCodeJson = "{}"
                }
            }
        });
        await context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        SqlConnection.ClearPool(new SqlConnection(_connectionString));

        if (!_databaseName.StartsWith(NamePrefix, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            await ExecuteAsync(
                _masterConnectionString,
                $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}];");
        }
        catch
        {
            // The next run uses a new database name.
        }
    }

    private static bool Probe()
    {
        try
        {
            using var connection = new SqlConnection(MasterConnectionString(connectTimeout: 5));
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string MasterConnectionString(int connectTimeout = 15)
    {
        return new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB",
            InitialCatalog = "master",
            IntegratedSecurity = true,
            ConnectTimeout = connectTimeout,
            TrustServerCertificate = true
        }.ConnectionString;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
