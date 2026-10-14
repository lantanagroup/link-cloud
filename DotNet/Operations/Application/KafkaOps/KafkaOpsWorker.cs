namespace LantanaGroup.Link.Operations.Application.KafkaOps;

public sealed class KafkaOpsWorker : BackgroundService
{
    private readonly IKafkaOpsService _service;
    private readonly ILogger<KafkaOpsWorker> _logger;

    public KafkaOpsWorker(IKafkaOpsService service, ILogger<KafkaOpsWorker> logger)
    {
        _service = service;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sampledAt = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _service.TrackAsync(stoppingToken);
                // One topic read every 30 seconds feeds the bounded overview history ring.
                if (DateTimeOffset.UtcNow - sampledAt >= TimeSpan.FromSeconds(30))
                {
                    sampledAt = DateTimeOffset.UtcNow;
                    await _service.GetTopicsAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kafka change tracking poll failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
