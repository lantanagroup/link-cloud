using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

/// <summary>
/// Duplicate delivery of one measure report is ignored. Any other failure from that insert is not.
/// </summary>
[Trait("Category", "UnitTests")]
public class ReportPopulationDuplicateDetectionTests
{
    [Fact]
    public async Task Redelivery_OfTheSameMeasureReport_DoesNotDoubleCount()
    {
        await using var harness = await PopulationHarness.CreateAsync();
        var delivery = harness.Delivery("mr-1", 5);

        await harness.ApplyAsync(delivery);
        await harness.ApplyAsync(delivery);
        await harness.ApplyAsync(harness.Delivery("mr-2", 7));

        await harness.AssertTotalsAsync(expectedTotal: 12, expectedChildren: 2);
    }

    [Fact]
    public async Task SqliteDuplicate_UsesExtendedCode2067AndNamesTheColumns()
    {
        await using var harness = await PopulationHarness.CreateAsync();
        await harness.ApplyAsync(harness.Delivery("mr-1", 5));

        await using var scope = harness.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var group = await context.GroupPopulation.Include(g => g.MeasureReportPopulations).FirstAsync();
        context.MeasureReportPopulation.Add(new MeasureReportPopulation
        {
            GroupPopulationId = group.Id,
            MeasureReportId = group.MeasureReportPopulations.First().MeasureReportId,
            PopulationCount = 9
        });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var sqlite = Find<SqliteException>(exception);

        Assert.Equal(19, sqlite.SqliteErrorCode);
        Assert.Equal(2067, sqlite.SqliteExtendedErrorCode);
        Assert.Contains(
            "UNIQUE constraint failed: MeasureReportPopulation.GroupPopulationId, MeasureReportPopulation.MeasureReportId",
            sqlite.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SqlException2601_ForTheMeasureReportIndex_IsSwallowedWhenTheChildExists()
    {
        await using var harness = await PopulationHarness.CreateAsync();
        var delivery = harness.Delivery("mr-1", 5);
        await harness.ApplyAsync(delivery);

        harness.Fault.Exception = SaveFailed(CreateSqlException(
            2601,
            "Cannot insert duplicate key row in object 'dbo.MeasureReportPopulation' with unique index 'IX_MeasureReportPopulation_Group_MeasureReport'. The duplicate key value is (1, mr-1)."));

        await harness.ApplyAsync(delivery);

        await harness.AssertTotalsAsync(expectedTotal: 5, expectedChildren: 1);
    }

    [Fact]
    public async Task SqlException2601_ForADifferentIndex_IsNotSwallowed()
    {
        await using var harness = await PopulationHarness.CreateAsync();
        var delivery = harness.Delivery("mr-1", 5);
        await harness.ApplyAsync(delivery);

        harness.Fault.Exception = SaveFailed(CreateSqlException(
            2601,
            "Cannot insert duplicate key row in object 'dbo.MeasureReportPopulation' with unique index 'IX_Test_Group_Count'. The duplicate key value is (1, 5)."));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => harness.ApplyAsync(delivery));
        Assert.Equal(2601, Find<SqlException>(exception).Number);
        await harness.AssertTotalsAsync(expectedTotal: 5, expectedChildren: 1);
    }

    [Fact]
    public async Task SqlException2627_ForADifferentConstraint_IsNotSwallowed()
    {
        await using var harness = await PopulationHarness.CreateAsync();
        var delivery = harness.Delivery("mr-1", 5);
        await harness.ApplyAsync(delivery);

        harness.Fault.Exception = SaveFailed(CreateSqlException(
            2627,
            "Violation of PRIMARY KEY constraint 'PK__MeasureR__3214EC07051D5E54'. Cannot insert duplicate key in object 'dbo.MeasureReportPopulation'. The duplicate key value is (1)."));

        await Assert.ThrowsAsync<DbUpdateException>(() => harness.ApplyAsync(delivery));
        await harness.AssertTotalsAsync(expectedTotal: 5, expectedChildren: 1);
    }

    [Fact]
    public async Task MessageThatMentions2601_IsNotSwallowed()
    {
        await using var harness = await PopulationHarness.CreateAsync();
        var delivery = harness.Delivery("mr-1", 5);
        await harness.ApplyAsync(delivery);

        harness.Fault.Exception = SaveFailed(new InvalidOperationException(
            "provider failed with 2601 and 2627 UNIQUE constraint failed: MeasureReportPopulation.GroupPopulationId, MeasureReportPopulation.MeasureReportId"));

        await Assert.ThrowsAsync<DbUpdateException>(() => harness.ApplyAsync(delivery));
        await harness.AssertTotalsAsync(expectedTotal: 5, expectedChildren: 1);
    }

    [Fact]
    public async Task NamedIndexWithoutAStoredChild_IsNotSwallowed()
    {
        await using var harness = await PopulationHarness.CreateAsync();
        harness.Fault.Exception = SaveFailed(CreateSqlException(
            2601,
            "Cannot insert duplicate key row in object 'dbo.MeasureReportPopulation' with unique index 'IX_MeasureReportPopulation_Group_MeasureReport'. The duplicate key value is (1, mr-1)."));

        await Assert.ThrowsAsync<DbUpdateException>(() => harness.ApplyAsync(harness.Delivery("mr-1", 5)));
        await harness.AssertNoChildrenAsync();
    }

    [Theory]
    [InlineData(787)]
    [InlineData(1299)]
    public async Task SqliteNonUniqueConstraint_IsNotSwallowed(int extendedErrorCode)
    {
        await using var harness = await PopulationHarness.CreateAsync();
        harness.Fault.Exception = SaveFailed(new SqliteException(
            "UNIQUE constraint failed: MeasureReportPopulation.GroupPopulationId, MeasureReportPopulation.MeasureReportId 2601 2627",
            19,
            extendedErrorCode));

        await Assert.ThrowsAsync<DbUpdateException>(() => harness.ApplyAsync(harness.Delivery("mr-1", 5)));
        await harness.AssertNoChildrenAsync();
    }

    private static DbUpdateException SaveFailed(Exception inner)
    {
        return new DbUpdateException("An error occurred while saving the entity changes.", inner);
    }

    private static T Find<T>(Exception exception) where T : Exception
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is T found)
                return found;
        }

        Assert.Fail(exception.ToString());
        return null!;
    }

    private static SqlException CreateSqlException(int number, string message)
    {
        var errorConstructor = typeof(SqlError).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            new[]
            {
                typeof(int), typeof(byte), typeof(byte), typeof(string),
                typeof(string), typeof(string), typeof(int), typeof(Exception)
            },
            modifiers: null);
        Assert.NotNull(errorConstructor);

        var error = errorConstructor!.Invoke(new object?[]
        {
            number, (byte)1, (byte)14, "localdb", message, string.Empty, 1, null
        });

        var errors = Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true);
        Assert.NotNull(errors);

        var add = typeof(SqlErrorCollection).GetMethod(
            "Add",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            new[] { typeof(SqlError) },
            modifiers: null);
        Assert.NotNull(add);
        add!.Invoke(errors, new[] { error });

        var create = typeof(SqlException).GetMethod(
            "CreateException",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            new[] { typeof(SqlErrorCollection), typeof(string) },
            modifiers: null);
        Assert.NotNull(create);

        return (SqlException)create!.Invoke(null, new[] { errors, "15.0.2000.5" })!;
    }

    private sealed class ChildInsertFault : SaveChangesInterceptor
    {
        public Exception? Exception { get; set; }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            ThrowIfChildInsert(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ThrowIfChildInsert(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void ThrowIfChildInsert(DbContext? context)
        {
            if (Exception is null || context is null)
                return;

            var insertingChild = context.ChangeTracker.Entries<MeasureReportPopulation>()
                .Any(entry => entry.State == EntityState.Added);

            if (insertingChild)
                throw Exception;
        }
    }

    private sealed class PopulationHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly string _path;

        private PopulationHarness(ServiceProvider provider, ChildInsertFault fault, string path, Guid scheduleId)
        {
            _provider = provider;
            Fault = fault;
            _path = path;
            ScheduleId = scheduleId;
        }

        public ChildInsertFault Fault { get; }

        public Guid ScheduleId { get; }

        public const string FacilityId = "fac-dup";

        public const string ReportType = "dup-type";

        public static async Task<PopulationHarness> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"report-pop-dup-{Guid.NewGuid():N}.db");
            var scheduleId = Guid.NewGuid();
            var fault = new ChildInsertFault();
            var services = new ServiceCollection();
            services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
            services.AddDbContext<ReportDbContext>(options =>
            {
                options.UseSqlite($"Data Source={path};Pooling=False");
                options.AddInterceptors(fault);
            });
            services.AddScoped<IReportPopulationManager, ReportPopulationManager>();
            var provider = services.BuildServiceProvider();

            await using (var scope = provider.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
                await context.Database.EnsureCreatedAsync();
                context.ReportSchedule.Add(new ReportSchedule
                {
                    Id = scheduleId,
                    FacilityId = FacilityId,
                    CreateDate = DateTime.UtcNow,
                    ReportStartDate = DateTimeOffset.UtcNow.AddDays(-7),
                    ReportEndDate = DateTimeOffset.UtcNow.AddDays(7),
                    EnableSubmission = true,
                    Frequency = 0,
                    Status = 0
                });
                await context.SaveChangesAsync();
            }

            return new PopulationHarness(provider, fault, path, scheduleId);
        }

        public AsyncServiceScope CreateScope()
        {
            return _provider.CreateAsyncScope();
        }

        public AggregateMeasureReportResult Delivery(string measureReportId, int count)
        {
            return new AggregateMeasureReportResult
            {
                Measure = "measure",
                ReportType = ReportType,
                MeasureReportId = measureReportId,
                PopulationList = new List<AggregateMeasureReportPopulation>
                {
                    new()
                    {
                        PopulationId = "initial-population",
                        PopulationCount = count,
                        PopulationCode = new CodeableConcept("http://example.com", "IP")
                    },
                    new()
                    {
                        PopulationId = "denominator",
                        PopulationCount = count,
                        PopulationCode = new CodeableConcept("http://example.com", "DENOM")
                    }
                }
            };
        }

        public async Task ApplyAsync(AggregateMeasureReportResult delivery)
        {
            await using var scope = _provider.CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();
            await manager.ApplyAggregateResultAsync(FacilityId, ScheduleId, delivery, CancellationToken.None);
        }

        public async Task AssertTotalsAsync(int expectedTotal, int expectedChildren)
        {
            await using var scope = _provider.CreateAsyncScope();
            var read = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
            var population = await read.ReportPopulation
                .AsNoTracking()
                .Include(p => p.GroupPopulations)
                .ThenInclude(g => g.MeasureReportPopulations)
                .SingleAsync(p => p.ReportScheduleId == ScheduleId && p.ReportType == ReportType);

            Assert.Equal(2, population.GroupPopulations.Count);
            foreach (var group in population.GroupPopulations)
            {
                Assert.Equal(expectedTotal, group.TotalPopulationCount);
                Assert.Equal(expectedChildren, group.MeasureReportPopulations.Count);
            }
        }

        public async Task AssertNoChildrenAsync()
        {
            await using var scope = _provider.CreateAsyncScope();
            var read = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
            var children = await read.MeasureReportPopulation.CountAsync();
            var total = await read.GroupPopulation.SumAsync(g => (int?)g.TotalPopulationCount) ?? 0;

            Assert.Equal(0, children);
            Assert.Equal(0, total);
        }

        public async ValueTask DisposeAsync()
        {
            await _provider.DisposeAsync();
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-journal", "-wal", "-shm" })
            {
                var file = _path + suffix;
                if (File.Exists(file))
                    File.Delete(file);
            }
        }
    }
}
