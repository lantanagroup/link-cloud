using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

/// <summary>
/// Stores per-type resource counts for logs that have resource ids but no counts, such as logs the previous version
/// completed after the database update created the counts table but before this version replaced it.
/// </summary>
/// <remarks>
/// Registered in both the Data Acquisition service and the acquisition worker, because during a rolling deployment
/// old worker pods keep completing logs until each is replaced. It fills once at startup and once more an hour later,
/// for logs old pods completed after this one started, however long ago the database was updated.
/// Needed only until every environment has started a version that writes the counts; remove it after that.
/// The migration that created the counts table filled them for all earlier logs.
/// </remarks>
public class ResourceTypeCountFillService : BackgroundService
{
    private static readonly TimeSpan DefaultSecondPassDelay = TimeSpan.FromHours(1);

    // The table's create_date is in the database server's local time, not UTC; a day covers any offset. Starting
    // earlier than needed costs nothing, because only logs without counts are filled.
    private static readonly TimeSpan CreatedAtMargin = TimeSpan.FromDays(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ResourceTypeCountFillService> _logger;
    private readonly TimeSpan _secondPassDelay;

    /// <summary>
    /// Creates the service. <paramref name="secondPassDelay"/> defaults to one hour.
    /// </summary>
    public ResourceTypeCountFillService(IServiceScopeFactory scopeFactory,
                                        ILogger<ResourceTypeCountFillService> logger,
                                        TimeSpan? secondPassDelay = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _secondPassDelay = secondPassDelay ?? DefaultSecondPassDelay;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await FillAsync(1, stoppingToken);
            await Task.Delay(_secondPassDelay, stoppingToken);
            await FillAsync(2, stoppingToken);
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

    private async Task FillAsync(int pass, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
        var manager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();

        var createdAt = await queries.GetResourceTypeCountsCreatedAtAsync(stoppingToken);
        if (createdAt is null)
        {
            _logger.LogInformation("The resource-type counts table does not exist yet; nothing to fill.");
            return;
        }

        var filled = await manager.FillMissingResourceTypeCountsAsync(createdAt.Value - CreatedAtMargin, stoppingToken);

        _logger.LogInformation("Pass {Pass}: stored resource-type counts for {LogCount} logs that had none.",
                               pass,
                               filled);
    }
}
