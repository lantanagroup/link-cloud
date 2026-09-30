using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Shared.Application.Health;

public sealed class DatabaseHealthCheck<TContext> : IHealthCheck
    where TContext : DbContext
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);
    private readonly TContext _dbContext;

    public DatabaseHealthCheck(TContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            timeoutCancellation.CancelAfter(ProbeTimeout);
            var canConnect = await _dbContext.Database.CanConnectAsync(timeoutCancellation.Token);
            timeoutCancellation.Token.ThrowIfCancellationRequested();
            cancellationToken.ThrowIfCancellationRequested();
            return canConnect ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Database connection failed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy($"Health check did not complete within {ProbeTimeout.TotalSeconds} seconds.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(description: $"Exception occurred while checking database health.", exception: ex);
        }
    }
}
