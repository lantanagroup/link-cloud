using LantanaGroup.Link.Shared.Application.Health;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared;

[Trait("Category", "UnitTests")]
public sealed class DatabaseHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseIsReachable_ReturnsHealthy()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        var check = new DatabaseHealthCheck<TestDbContext>(context);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseIsUnreachable_ReturnsUnhealthy()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "health.db");
        await using var context = CreateContext($"Data Source={databasePath}");
        var check = new DatabaseHealthCheck<TestDbContext>(context);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenCallerCancels_PropagatesCancellation()
    {
        await using var context = CreateContext("Data Source=:memory:");
        var check = new DatabaseHealthCheck<TestDbContext>(context);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            check.CheckHealthAsync(new HealthCheckContext(), cancellation.Token));
    }

    /// <summary>
    /// The name a check is registered under is the label the Admin UI's Health Checks page renders
    /// for the row, so a database row has to name the provider it reached rather than read
    /// "Database" beside MeasureEval's MongoDB row and leave the two uncomparable.
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_NamesTheDatabaseEntryForItsProvider()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TestDbContext>(options => options.UseSqlite(connection));
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck<TestDbContext>>(HealthCheckNames.SqlServerDatabase);

        await using var provider = services.BuildServiceProvider();

        var report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        Assert.Equal("Database (SQL Server)", Assert.Single(report.Entries).Key);
    }

    private static TestDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options);

    private static TestDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connectionString).Options);

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);
}