using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

/// <summary>
/// Once at startup, stores per-type resource counts for recently modified logs that have none, such as logs the
/// previous version completed between the database update and this version starting.
/// </summary>
/// <remarks>
/// Registered in both the Data Acquisition service and the acquisition worker, because during a rolling deployment
/// old worker pods keep completing logs until each is replaced; every new pod fills what was completed before it.
/// Needed only until every environment has started a version that writes the counts; remove it after that.
/// The migration that created the counts table filled them for all earlier logs.
/// </remarks>
public class ResourceTypeCountFillService : BackgroundService
{
    // Long enough to cover a release rollout; older logs were filled by the migration that added the counts.
    private static readonly TimeSpan Lookback = TimeSpan.FromDays(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ResourceTypeCountFillService> _logger;

    /// <summary>
    /// Creates the service.
    /// </summary>
    public ResourceTypeCountFillService(IServiceScopeFactory scopeFactory, ILogger<ResourceTypeCountFillService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();

            var filled = await manager.FillMissingResourceTypeCountsAsync(DateTime.UtcNow - Lookback, stoppingToken);

            _logger.LogInformation("Stored resource-type counts for {LogCount} recently modified logs that had none.",
                                   filled);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Stopped before missing resource-type counts were filled.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                               "Could not fill missing resource-type counts. Query Analysis may undercount logs " +
                               "completed during the last deployment until the service starts again.");
        }
    }
}
