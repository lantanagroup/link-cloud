using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Submission.Data;

namespace LantanaGroup.Link.Submission.Application.HealthChecks;

public class DatabaseHealthCheck(SubmissionContext context) : IHealthCheck
{
    private readonly SubmissionContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy(nameof(HealthCheckType.Database))
                : HealthCheckResult.Unhealthy(nameof(HealthCheckType.Database));
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }
}