using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Services;

public class SftpAcquisitionService(
    ILogger<SftpAcquisitionService> logger,
    IServiceScopeFactory scopeFactory,
    IOptions<SftpAcquisitionSettings> settings) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SftpAcquisitionService started with interval of {Interval} seconds",
            settings.Value.JobIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<SftpAcquisitionHandler>();
                await handler.ExecuteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during SFTP acquisition processing cycle");
            }

            await Task.Delay(TimeSpan.FromSeconds(settings.Value.JobIntervalSeconds), stoppingToken);
        }

        logger.LogInformation("SftpAcquisitionService stopped");
    }
}
