using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.Security;
using System.Collections.Concurrent;


namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{

    public class KafkaConsumerManager
    {

        private ConcurrentBag<(IConsumer<string, string>, CancellationTokenSource)> _consumers;
        private readonly KafkaConnection _kafkaConnection;
        private readonly KafkaConsumerService _kafkaConsumerService;

        private readonly static string errorTopic = "-Error";
        public static readonly string delimiter = ":";
        public static readonly string consumers = "consumers";
        private static readonly object _lock = new object();

        private readonly ILogger<KafkaConsumerService> _logger;
        private readonly ICacheService _cache;

        // construct a list of topics 
        private List<(string, string)> kafkaTopics = new List<(string, string)>
          {
            ("Dynamic", KafkaTopic.ReportScheduled.ToString()),
            ("Dynamic", KafkaTopic.ReportScheduled.ToString() + errorTopic),
            ("Dynamic", KafkaTopic.PatientIDsAcquired.ToString()),
            ("Dynamic", KafkaTopic.PatientIDsAcquired.ToString() + errorTopic),
            ("Dynamic", KafkaTopic.PatientEvent.ToString()),
            ("Dynamic", KafkaTopic.PatientEvent.ToString() + errorTopic),
            ("Dynamic", KafkaTopic.DataAcquisitionRequested.ToString()),
            ("Dynamic", KafkaTopic.DataAcquisitionRequested.ToString() + errorTopic),
            ("Dynamic", KafkaTopic.ReadyToAcquire.ToString()),
            ("Dynamic", KafkaTopic.ReadyToAcquire.ToString() + errorTopic),
            ("Dynamic", KafkaTopic.ResourceAcquired.ToString()),
            ("Dynamic", KafkaTopic.ResourceAcquired.ToString() + errorTopic),
            ("Dynamic", KafkaTopic.ResourceNormalized.ToString()),
            ("Dynamic", KafkaTopic.ResourceNormalized.ToString() + errorTopic),
            ("Dynamic", KafkaTopic.ResourceEvaluated.ToString()),
            ("Dynamic", KafkaTopic.ResourceEvaluated.ToString() + errorTopic),
            ("Dynamic", KafkaTopic.ReadyForValidation.ToString()),
            ("Dynamic", KafkaTopic.ReadyForValidation.ToString() + errorTopic),
            ("Dynamic", KafkaTopic.ValidationComplete.ToString()),
            ("Dynamic", KafkaTopic.ValidationComplete.ToString() + errorTopic),
            ("Dynamic", KafkaTopic.SubmitPayload.ToString()),
            ("Dynamic", KafkaTopic.SubmitPayload.ToString() + errorTopic),
            ("Dynamic", KafkaTopic.PayloadSubmitted.ToString()),
            ("Dynamic", KafkaTopic.PayloadSubmitted.ToString() + errorTopic)
          };



        // Add constructor
        public KafkaConsumerManager(KafkaConsumerService kafkaConsumerService, ICacheService cache, KafkaConnection kafkaConnection, ILogger<KafkaConsumerService> logger)
        {
            _kafkaConsumerService = kafkaConsumerService;
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

                _logger.LogError(ex, "Failed to clear cache for facility {Facility} due to invalid operation", HtmlInputSanitizer.SanitizeAndRemove(facility));
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while clearing cache for facility {Facility}", HtmlInputSanitizer.SanitizeAndRemove(facility));
            }
        }


        // Remove consumers based on facility using lock to avoid concurrent access to the bag
        private void RemoveConsumersBasedOnFacility(ConcurrentBag<(IConsumer<string, string>, CancellationTokenSource)> bag, string facility)
        {
            lock (_lock)
            {
                var newBag = new ConcurrentBag<(IConsumer<string, string>, CancellationTokenSource)>();
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
                AutoOffsetReset = AutoOffsetReset.Latest,
                SessionTimeoutMs = 10000,
                HeartbeatIntervalMs = 3000
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

        public async Task StopAllConsumers(string facility)
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

            await DeleteConsumerGroupAsync(string.Join(", ", _kafkaConnection.BootstrapServers), "Dynamic:" + facility);

            _logger.LogInformation("All Groups have been deleted");

        }


        public async Task<bool> DeleteConsumerGroupAsync(string bootstrapServers, string groupId, int maxWaitTimeInSeconds = 60, CancellationToken cancellationToken = default)
        {
            var config = new AdminClientConfig { BootstrapServers = bootstrapServers };

            int delaySeconds = 1; // Start with 1 second
            int maxDelaySeconds = 30; // Cap to avoid very long delays

            using (var adminClient = new AdminClientBuilder(config).Build())
            {
                try
                {
                    DateTime startTime = DateTime.UtcNow;
                    bool isGroupEmpty = false;

                    while ((DateTime.UtcNow - startTime).TotalSeconds < maxWaitTimeInSeconds)
                    {
                        var groupDescription = await adminClient.DescribeConsumerGroupsAsync(new List<string> { groupId });

                        // If the group does not exist, treat as success
                        if (groupDescription.ConsumerGroupDescriptions.Any(g => g.Error.Code == ErrorCode.GroupIdNotFound))
                        {
                            _logger.LogInformation("Consumer group {GroupId} does not exist. Nothing to delete.",
                                HtmlInputSanitizer.SanitizeAndRemove(groupId));
                            return true;
                        }


                        isGroupEmpty = groupDescription.ConsumerGroupDescriptions.All(g => g.Members.Count == 0);

                        if (isGroupEmpty) break;

                        _logger.LogInformation("Consumer group {GroupId} still active. Retrying in {Interval}s...",
                            HtmlInputSanitizer.SanitizeAndRemove(groupId), delaySeconds);

                        await Task.Delay(delaySeconds * 1000);

                        // Increase delay using exponential backoff
                        delaySeconds = Math.Min(delaySeconds * 2, maxDelaySeconds);
                    }

                    if (!isGroupEmpty)
                    {
                        _logger.LogWarning("Timeout waiting for consumer group {GroupId} to become empty.",
                            HtmlInputSanitizer.SanitizeAndRemove(groupId));
                        return false;
                    }

                    _logger.LogInformation("Deleting consumer group {GroupId}...", HtmlInputSanitizer.SanitizeAndRemove(groupId));
                    try
                    {
                        await adminClient.DeleteGroupsAsync(new List<string> { groupId });
                        _logger.LogInformation("Consumer group {GroupId} deleted successfully.", HtmlInputSanitizer.SanitizeAndRemove(groupId));
                    }
                    catch (KafkaException ex) when (ex.Error.Code == ErrorCode.GroupIdNotFound)
                    {
                        _logger.LogInformation("Consumer group {GroupId} already deleted.", HtmlInputSanitizer.SanitizeAndRemove(groupId));
                    }

                    return true;
                }
                catch (KafkaException ex)
                {
                    _logger.LogError("Kafka error deleting consumer group {GroupId}: {Message}",
                        HtmlInputSanitizer.SanitizeAndRemove(groupId), ex.Message);
                }
                catch (TimeoutException ex)
                {
                    _logger.LogError("Timeout deleting consumer group {GroupId}: {Message}",
                        HtmlInputSanitizer.SanitizeAndRemove(groupId), ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Unexpected error deleting consumer group {GroupId}: {Message}",
                        HtmlInputSanitizer.SanitizeAndRemove(groupId), ex.Message);
                }
            }

            return false; // In case of failure, return false
        }

    }

}
