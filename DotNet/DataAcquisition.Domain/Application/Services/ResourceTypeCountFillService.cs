using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

/// <summary>
/// Stores per-type resource counts for logs that have resource ids but no counts, such as logs the previous version
/// completed after the database update created the counts table, including while its pods were being replaced.
/// </summary>
/// <remarks>
/// Registered in both the Data Acquisition service and the acquisition worker. The first pass covers every log
/// modified since the counts table was created; later passes run every hour for as long as the process runs and cover
/// logs modified since the previous successful pass. An old pod that completes a log after a pass stamps it with its
/// completion time, so the next pass in any running new pod fills it, however the rollout is ordered.
/// Needed only until every environment has started a version that writes the counts; remove it after that.
/// The migration that created the counts table filled them for all earlier logs.
/// </remarks>
public class ResourceTypeCountFillService : BackgroundService
{
    private static readonly TimeSpan DefaultPassInterval = TimeSpan.FromHours(1);

    // The table's create_date is in the database server's local time, not UTC; a day covers any offset. Starting
    // earlier than needed costs nothing, because only logs without counts are filled.
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
                    if (await FillAsync(modifiedSince, stoppingToken))
                    {
                        modifiedSince = passStarted - PassOverlap;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException ||
                                           !stoppingToken.IsCancellationRequested)
                {
                    // The next pass retries the same window.
                    _logger.LogWarning(ex,
                                       "Could not fill missing resource-type counts; retrying in {Interval}.",
                                       _passInterval);
                }

                await Task.Delay(_passInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Stopped filling missing resource-type counts.");
        }
    }

    // Returns false when there was nothing to fill from yet, so the next pass starts from the table's creation again.
    private async Task<bool> FillAsync(DateTime? modifiedSince, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
        var manager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();

        if (modifiedSince is null)
        {
            var createdAt = await queries.GetResourceTypeCountsCreatedAtAsync(stoppingToken);
            if (createdAt is null)
            {
                _logger.LogInformation("The resource-type counts table does not exist yet; nothing to fill.");
                return false;
            }

            modifiedSince = createdAt.Value - CreatedAtMargin;
        }

        var filled = await manager.FillMissingResourceTypeCountsAsync(modifiedSince.Value, stoppingToken);

        if (filled > 0)
        {
            _logger.LogInformation("Stored resource-type counts for {LogCount} logs that had none.", filled);
        }
        else
        {
            _logger.LogDebug("No logs were missing resource-type counts.");
        }

        return true;
    }
}
