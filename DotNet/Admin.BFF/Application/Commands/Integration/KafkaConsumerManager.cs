using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using System.Collections.Concurrent;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{

    public class KafkaConsumerManager
    {

        private ConcurrentBag<(IConsumer<string, string>, CancellationTokenSource)> _consumers;
        private readonly KafkaConnection _kafkaConnection;
        private readonly KafkaConsumerService _kafkaConsumerService;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        private readonly static string errorTopic = "-Error";
        public static readonly string delimiter = ":";
        public static readonly string consumers = "consumers";
        private static readonly object _lock = new object();

        private readonly ILogger<KafkaConsumerService> _logger;
        private readonly ICacheService _cache;

        // construct a list of topics 
        private List<(string, string)> kafkaTopics = new List<(string, string)>
          {
            ("ReportScheduledDyn", KafkaTopic.ReportScheduled.ToString()),
            ("ReportScheduledDyn", KafkaTopic.ReportScheduled.ToString() + errorTopic),
            ("CensusDyn", KafkaTopic.PatientIDsAcquired.ToString()),
            ("CensusDyn", KafkaTopic.PatientIDsAcquired.ToString() + errorTopic),
            ("QueryDispatchDyn", KafkaTopic.PatientEvent.ToString()),
            ("QueryDispatchDyn", KafkaTopic.PatientEvent.ToString() + errorTopic),
            ("DataAcquisitionDyn", KafkaTopic.DataAcquisitionRequested.ToString()),
            ("DataAcquisitionDyn", KafkaTopic.DataAcquisitionRequested.ToString() + errorTopic),
            ("AcquiredDyn", KafkaTopic.ResourceAcquired.ToString()),
            ("AcquiredDyn", KafkaTopic.ResourceAcquired.ToString() + errorTopic),
            ("NormalizationDyn", KafkaTopic.ResourceNormalized.ToString()),
            ("NormalizationDyn", KafkaTopic.ResourceNormalized.ToString() + errorTopic),
            ("ResourceEvaluatedDyn", KafkaTopic.ResourceEvaluated.ToString()),
            ("ResourceEvaluatedDyn", KafkaTopic.ResourceEvaluated.ToString() + errorTopic),
            ("ReportDyn", KafkaTopic.SubmitReport.ToString()),
            ("ReportDyn", KafkaTopic.SubmitReport.ToString() + errorTopic),
          };



        // Add constructor
        public KafkaConsumerManager(KafkaConsumerService kafkaConsumerService, ICacheService cache, IServiceScopeFactory serviceScopeFactory, KafkaConnection kafkaConnection, ILogger<KafkaConsumerService> logger)
        {
            _kafkaConsumerService = kafkaConsumerService;
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _consumers = new ConcurrentBag<(IConsumer<string, string>, CancellationTokenSource)>();
            _kafkaConnection = kafkaConnection ?? throw new ArgumentNullException(nameof(_kafkaConnection));
            _cache = cache;
            _logger = logger;
        }


        private void ClearCache(string facility)
        {
            try
            {
                foreach (var topic in kafkaTopics)
                {
                    {
                        String cacheKey = topic.Item2 + delimiter + facility;
                        _cache.Remove(cacheKey);
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Failed to clear cache for facility {facility} due to invalid operation", facility);
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while clearing cache for facility {facility}", facility);
            }
        }


        // Remove consumers based on facility using lock to avoid concurrent access to the bag
        private void RemoveConsumersBasedOnFacility(ConcurrentBag<(IConsumer<string, string>, CancellationTokenSource)> bag, string facility)
        {
            lock (_lock)
            {
                var newBag = new ConcurrentBag < (IConsumer<string, string>, CancellationTokenSource) > ();
                foreach (var item in bag)
                {
                    if (!item.Item1.Name.Contains(facility)) // Keep items that do not match the condition
                    {
                        newBag.Add(item);
                    }
                }

                // Replace the old bag
                while (bag.TryTake(out _)) { } // Clear the old bag
                foreach (var item in newBag)
                {
                    bag.Add(item); // Add the filtered items back to the original bag
                }
            }
        }

        public void CreateAllConsumers(string facility)
        {
            //clear  cache for that facility
            ClearCache(facility);

            // create consumers

            foreach (var topic in kafkaTopics)
            {
                if (topic.Item2 != string.Empty)
                {
                    CreateConsumer(topic.Item1, topic.Item2, facility);
                }
            }
        }


        public void CreateConsumer(string groupId, string topic, string facility)
        {
            var cts = new CancellationTokenSource();
            var config = new ConsumerConfig
            {
                GroupId = groupId + delimiter + facility,
                ClientId = facility,
                BootstrapServers = string.Join(", ", _kafkaConnection.BootstrapServers),
                AutoOffsetReset = AutoOffsetReset.Latest
            };
 
            if (_kafkaConnection.SaslProtocolEnabled)
            {
                config.SecurityProtocol = SecurityProtocol.SaslPlaintext;
                config.SaslMechanism = SaslMechanism.Plain;
                config.SaslUsername = _kafkaConnection.SaslUsername;
                config.SaslPassword = _kafkaConnection.SaslPassword;
            }

            var consumer = new ConsumerBuilder<string, string>(config).Build();

            _consumers.Add((consumer, cts));

            Task.Run(() => _kafkaConsumerService.StartConsumer(groupId, topic, facility, consumer, cts.Token));

        }


        public Dictionary<string, string> readAllConsumers(string facility)
        {
            Dictionary<string, string> correlationIds = new Dictionary<string, string>();

            // loop through the  keys for that facility and get the correlation id for each
            foreach (var topic in kafkaTopics)
            {
                if (topic.Item2 != string.Empty)
                {
                    string facilityKey = topic.Item2 + delimiter + facility;

                    correlationIds.Add(topic.Item2, _cache.Get<string>(facilityKey));
  
                }
            }
            return correlationIds;
        }

        public void StopAllConsumers(string facility)
        {
            //clear  cache for that facility
            ClearCache(facility);

            // stop consumers for that facility
            foreach (var consumer in _consumers)
            {

                if (consumer.Item1.Name.Contains(facility))
                {
                    _logger.LogInformation($"Type of Item2: {consumer.Item2.GetType()}");
                    if (consumer.Item2 != null && consumer.Item2 is CancellationTokenSource cts && !cts.IsCancellationRequested)
                    {
                        try
                        {
                            consumer.Item2.Cancel();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInformation($"Error during cancellation: {ex.Message}");

                        }
                    }
                    else
                    {
                        _logger.LogInformation("CancellationTokenSource is already disposed or canceled.");
                    }
                 
                }
            }

            // remove only consumers for that facility
            RemoveConsumersBasedOnFacility(_consumers, facility);

        }

    }

}
