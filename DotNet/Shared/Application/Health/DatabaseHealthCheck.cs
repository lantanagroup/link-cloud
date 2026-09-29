using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Shared.Application.Health;

public sealed class DatabaseHealthCheck<TContext> : IHealthCheck
    where TContext : DbContext
{
    private readonly TContext _dbContext;

    public DatabaseHealthCheck(TContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return canConnect ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }
}
