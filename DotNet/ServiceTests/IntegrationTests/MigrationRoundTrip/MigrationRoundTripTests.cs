using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ServiceTests.TestHelpers;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.MigrationRoundTrip;

/// <summary>
/// Runs every service's migrations against an empty SQL Server database, the way a new environment gets its
/// schema at startup, and proves the latest migration rolls back and applies again.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "IntegrationTests")]
public sealed class MigrationRoundTripTests
{
    private const string QuartzColumnMigrationName = "AddQuartzMisfireOrigFireTime";

    private const string CountQuartzColumnSql =
        "SELECT COUNT(*) AS [Value] FROM sys.columns " +
        "WHERE [object_id] = OBJECT_ID(N'quartz.QRTZ_TRIGGERS') AND [name] = N'MISFIRE_ORIG_FIRE_TIME'";

    private const string AddQuartzColumnByHandSql =
        "ALTER TABLE [quartz].[QRTZ_TRIGGERS] ADD [MISFIRE_ORIG_FIRE_TIME] bigint NULL;";

    private readonly MigrationSqlServerFixture _fixture;

    public MigrationRoundTripTests(MigrationSqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// The name of every DbContext that a service migrates at startup.
    /// </summary>
    public static TheoryData<string> ContextNames => new(AutoMigratedContexts.Names);

    /// <summary>
    /// The services whose migration adds the Quartz MISFIRE_ORIG_FIRE_TIME column only when it is missing.
    /// </summary>
    public static TheoryData<string> QuartzColumnContextNames => new("Census", "QueryDispatch", "Report", "Submission");

    [Theory]
    [MemberData(nameof(ContextNames))]
    public async Task Migrate_EmptyDatabase_AppliesEveryMigrationAndRollsBackTheLatest(string contextName)
    {
        var connectionString = await _fixture.CreateDatabaseAsync($"MigrationTest_{contextName}");

        try
        {
            await using var context = AutoMigratedContexts.Create(contextName, connectionString);
            var migrator = context.Database.GetService<IMigrator>();
            var migrations = context.Database.GetMigrations().ToList();
            var previousMigration = migrations.Count > 1 ? migrations[^2] : Migration.InitialDatabase;

            await context.Database.MigrateAsync();
            Assert.Equal(migrations, await GetAppliedMigrationsAsync(context));

            await migrator.MigrateAsync(previousMigration);
            Assert.Equal(migrations.SkipLast(1), await GetAppliedMigrationsAsync(context));

            await migrator.MigrateAsync();
            Assert.Equal(migrations, await GetAppliedMigrationsAsync(context));
        }
        finally
        {
            await _fixture.DropDatabaseAsync(connectionString);
        }
    }

    [Theory]
    [MemberData(nameof(QuartzColumnContextNames))]
    public async Task QuartzColumnMigration_ColumnMissing_AddsItAndRollbackRemovesIt(string contextName)
    {
        var connectionString = await _fixture.CreateDatabaseAsync($"MigrationTest_{contextName}");

        try
        {
            await using var context = AutoMigratedContexts.Create(contextName, connectionString);
            var migrator = context.Database.GetService<IMigrator>();
            var (columnMigration, previousMigration) = FindQuartzColumnMigration(context);

            await migrator.MigrateAsync(previousMigration);
            Assert.Equal(0, await CountQuartzColumnAsync(context));

            await migrator.MigrateAsync(columnMigration);
            Assert.Equal(1, await CountQuartzColumnAsync(context));

            await migrator.MigrateAsync(previousMigration);
            Assert.Equal(0, await CountQuartzColumnAsync(context));
        }
        finally
        {
            await _fixture.DropDatabaseAsync(connectionString);
        }
    }

    [Theory]
    [MemberData(nameof(QuartzColumnContextNames))]
    public async Task QuartzColumnMigration_ColumnAddedByHand_AppliesWithoutErrorAndRollbackRemovesIt(string contextName)
    {
        var connectionString = await _fixture.CreateDatabaseAsync($"MigrationTest_{contextName}");

        try
        {
            await using var context = AutoMigratedContexts.Create(contextName, connectionString);
            var migrator = context.Database.GetService<IMigrator>();
            var (columnMigration, previousMigration) = FindQuartzColumnMigration(context);

            await migrator.MigrateAsync(previousMigration);
            await context.Database.ExecuteSqlRawAsync(AddQuartzColumnByHandSql);

            await migrator.MigrateAsync(columnMigration);
            Assert.Equal(1, await CountQuartzColumnAsync(context));
            Assert.Contains(columnMigration, await GetAppliedMigrationsAsync(context));

            await migrator.MigrateAsync(previousMigration);
            Assert.Equal(0, await CountQuartzColumnAsync(context));
        }
        finally
        {
            await _fixture.DropDatabaseAsync(connectionString);
        }
    }

    private static async Task<List<string>> GetAppliedMigrationsAsync(DbContext context)
    {
        return (await context.Database.GetAppliedMigrationsAsync()).ToList();
    }

    private static (string ColumnMigration, string PreviousMigration) FindQuartzColumnMigration(DbContext context)
    {
        var migrations = context.Database.GetMigrations().ToList();
        var columnMigration = migrations.Single(migration => migration.Contains(QuartzColumnMigrationName));

        return (columnMigration, migrations[migrations.IndexOf(columnMigration) - 1]);
    }

    private static async Task<int> CountQuartzColumnAsync(DbContext context)
    {
        return await context.Database
            .SqlQueryRaw<int>(CountQuartzColumnSql)
            .SingleAsync();
    }
}
