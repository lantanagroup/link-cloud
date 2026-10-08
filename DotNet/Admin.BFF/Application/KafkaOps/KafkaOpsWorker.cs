namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

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
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _service.TrackAsync(stoppingToken);
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
