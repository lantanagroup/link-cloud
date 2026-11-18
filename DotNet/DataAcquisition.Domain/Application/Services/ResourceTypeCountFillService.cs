using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

/// <summary>
/// Keeps the per-type resource counts of completed logs in step with their resource ids while a previous version,
/// which writes resource ids but not counts, may still be running alongside this one.
/// </summary>
/// <remarks>
/// Registered in both the Data Acquisition service and the acquisition worker. The first pass reconciles every
/// completed log modified since the counts table was created; later passes run every hour for as long as the process
/// runs and reconcile logs modified since the previous successful pass. An old pod that completes or re-processes a
/// log stamps it with that time, so the next pass in any running new pod replaces its missing or stale counts,
/// however the rollout is ordered.
/// Needed only until every environment has started a version that writes the counts; remove it after that.
/// The migration that created the counts table filled them for all earlier logs.
/// </remarks>
public class ResourceTypeCountFillService : BackgroundService
{
    private static readonly TimeSpan DefaultPassInterval = TimeSpan.FromHours(1);

    // The table's create_date is in the database server's local time, not UTC; a day covers any offset. Starting
    // earlier than needed only rechecks logs whose counts are already right.
    private static readonly TimeSpan CreatedAtMargin = TimeSpan.FromDays(1);

    // Later passes reach back past the previous pass's start to cover clock differences between pods and logs
    // whose resource ids were saved shortly after their ModifyDate was set.
    private static readonly TimeSpan PassOverlap = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ResourceTypeCountFillService> _logger;
    private readonly TimeSpan _passInterval;

    /// <summary>
    /// Creates the service. <paramref name="passInterval"/> defaults to one hour.
    /// </summary>
    public ResourceTypeCountFillService(IServiceScopeFactory scopeFactory,
                                        ILogger<ResourceTypeCountFillService> logger,
                                        TimeSpan? passInterval = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _passInterval = passInterval ?? DefaultPassInterval;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateTime? modifiedSince = null;

        try
        {
            while (true)
            {
                var passStarted = DateTime.UtcNow;

                try
                {
                    if (await ReconcileAsync(modifiedSince, stoppingToken))
                    {
                        modifiedSince = passStarted - PassOverlap;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException ||
                                           !stoppingToken.IsCancellationRequested)
                {
                    // The next pass retries the same window.
                    _logger.LogWarning(ex,
                                       "Could not reconcile resource-type counts; retrying in {Interval}.",
                                       _passInterval);
                }

                await Task.Delay(_passInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Stopped reconciling resource-type counts.");
        }
    }

    // Returns false when there was nothing to reconcile from yet, so the next pass starts from the table's creation.
    private async Task<bool> ReconcileAsync(DateTime? modifiedSince, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
        var manager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();

        if (modifiedSince is null)
        {
            var createdAt = await queries.GetResourceTypeCountsCreatedAtAsync(stoppingToken);
            if (createdAt is null)
            {
                _logger.LogInformation("The resource-type counts table does not exist yet; nothing to reconcile.");
                return false;
            }

            modifiedSince = createdAt.Value - CreatedAtMargin;
        }

        var replaced = await manager.ReconcileResourceTypeCountsAsync(modifiedSince.Value, stoppingToken);

        if (replaced > 0)
        {
            _logger.LogInformation("Replaced missing or stale resource-type counts for {LogCount} logs.", replaced);
        }
        else
        {
            _logger.LogDebug("No completed logs had missing or stale resource-type counts.");
        }

        return true;
    }
}
