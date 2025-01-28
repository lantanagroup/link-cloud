using Confluent.Kafka;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;


namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{
    public class KafkaConsumerService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<KafkaConsumerService> _logger;
        private readonly ICacheService _cache;


        public KafkaConsumerService(ICacheService cache, IServiceScopeFactory serviceScopeFactory, ILogger<KafkaConsumerService> logger)
        {

            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache;
        }

        public void StartConsumer(string groupId, string topic, string facility, IConsumer<string, string> consumer, CancellationToken cancellationToken)
        {

            // get the cache
            using var scope = _serviceScopeFactory.CreateScope();

            using (consumer)
            {
                consumer.Subscribe(topic);
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {

                        var consumeResult = consumer.Consume(cancellationToken);
                        // get the correlation id from the message and store it in Cache
                        string correlationId = string.Empty;
                        if (consumeResult.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                        {
                            correlationId = System.Text.Encoding.UTF8.GetString(headerValue);
                            string consumeResultFacility = this.extractFacility(consumeResult.Message.Key);

                            if (facility != consumeResultFacility)
                            {
                                _logger.LogInformation("Searched Facility ID {facility} does not match message facility {consumeResultFacility}. Skipping message.", facility, consumeResultFacility);
                                continue;
                            }
                            // read the list from cache
                            var cacheKey = topic + KafkaConsumerManager.delimiter + facility;

                            string retrievedListJson;
                            try 
                            {
                               retrievedListJson = _cache.Get<string>(cacheKey);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to retrieve correlation IDs from cache for key {key}", cacheKey);
                                retrievedListJson = null;
                            }

                            var retrievedList = string.IsNullOrEmpty(retrievedListJson) ? new List<string>() : JsonConvert.DeserializeObject<List<string>>(retrievedListJson);

                            // append the new correlation id to the existing list
                            if (!retrievedList.Contains(correlationId))
                            {
                                retrievedList.Add(correlationId);

                                string serializedList = JsonConvert.SerializeObject(retrievedList);

                                // store the list back in Cache
                                _cache.Set(cacheKey, serializedList, TimeSpan.FromMinutes(30));
                            }
                        }
                        _logger.LogInformation("Consumed message '{MessageValue}' from topic {Topic}, partition {Partition}, offset {Offset}, correlation {CorrelationId}", consumeResult.Message.Value, consumeResult.Topic, consumeResult.Partition, consumeResult.Offset, correlationId);
                    }
                }
                catch (ConsumeException e)
                {
                    if (e.ConsumerRecord != null)
                    {
                        _logger.LogError(e, "Error occurred during consumption. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}, Reason: {Reason}", e.ConsumerRecord.Topic, e.ConsumerRecord.Partition.Value, e.ConsumerRecord.Offset.Value, e.Error.Reason);
                    }
                    else
                    {
                        _logger.LogError(e, "Error occurred: {Reason}", e.Error.Reason);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Consumer {ConsumerName} stopped.", consumer.Name);
                }
            }
        }

        public string extractFacility(string kafkaKey)
        {
            if (string.IsNullOrEmpty(kafkaKey))
            {
                return "";
            }

            // Try to parse the key as JSON
            try
            {
                var jsonObject = JObject.Parse(kafkaKey);
                var matchingProperty = jsonObject.Properties().FirstOrDefault(p => Regex.IsMatch(p.Name, "facility", RegexOptions.IgnoreCase));

                if (matchingProperty != null)
                {
                    return matchingProperty.Value.ToString();
                }
                else
                {
                    return "";
                }
            }
            catch (JsonReaderException)
            {
                // If parsing fails, treat it as a plain string
                return kafkaKey;
            }
        }
    }

}
