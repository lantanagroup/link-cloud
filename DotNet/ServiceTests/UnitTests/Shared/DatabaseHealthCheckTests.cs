using LantanaGroup.Link.Shared.Application.Health;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

    private static TestDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options);

    private static TestDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connectionString).Options);

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);
}