
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using static Confluent.Kafka.ConfigPropertyNames;

namespace LantanaGroup.Link.DataAcquisition.Listeners;

public class RetryListener : BackgroundService
{
    private readonly ILogger<RetryListener> _logger;
    private readonly IKafkaConsumerFactory<string, string> _kafkaConsumerFactory;
    private readonly IRetryEntityFactory _retryEntityFactory;

    public RetryListener(ILogger<RetryListener> logger, IKafkaConsumerFactory<string, string> kafkaConsumerFactory, IRetryEntityFactory retryEntityFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentNullException(nameof(kafkaConsumerFactory));
        _retryEntityFactory = retryEntityFactory ?? throw new ArgumentNullException(nameof(retryEntityFactory));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
    }

    private async void StartConsumerLoop(CancellationToken cancellationToken)
    {
        var config = new ConsumerConfig
        {
            GroupId = "DataAcquisitionRetry",
            EnableAutoCommit = false,
        };

        using var _consumer = _kafkaConsumerFactory.CreateConsumer(config);

        try
        {
            _consumer.Subscribe(new List<string> { KafkaTopic.DataAcquisitionRequestedRetry.ToString(), KafkaTopic.PatientCensusScheduledRetry.ToString() });

            _logger.LogInformation("Started Data Acquisition Service Retry consumer for topics: [{1}] {2}", string.Join(", ", _consumer.Subscription), DateTime.UtcNow);
        }
        catch (OperationCanceledException oce)
        {
            _logger.LogError(oce, "Operation Cancelled: {1}", oce.Message);
            _consumer.Close();
            _consumer.Dispose();
        }
    }
}
