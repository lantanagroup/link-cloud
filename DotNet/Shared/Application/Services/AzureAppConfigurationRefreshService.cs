using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.Shared.Application.Services;

public class AzureAppConfigurationRefreshService : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    private readonly ILogger<AzureAppConfigurationRefreshService> _logger;
    private readonly IEnumerable<IConfigurationRefresher> _refreshers;

    public AzureAppConfigurationRefreshService(
        ILogger<AzureAppConfigurationRefreshService> logger,
        IEnumerable<IConfigurationRefresher> refreshers)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _refreshers = refreshers ?? throw new ArgumentNullException(nameof(refreshers));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var refresher in _refreshers)
            {
                try
                {
                    await refresher.TryRefreshAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to refresh Azure App Configuration.");
                }
            }

            await Task.Delay(RefreshInterval, stoppingToken);
        }
    }
}
