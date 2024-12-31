using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;


namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{
    public class KafkaConsumerService
    {
        private readonly IOptions<CacheSettings> _cacheSettings;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<KafkaConsumerService> _logger;

 

        public KafkaConsumerService( IOptions<CacheSettings> cacheSettings, IServiceScopeFactory serviceScopeFactory, ILogger<KafkaConsumerService> logger)
        {
            _cacheSettings = cacheSettings ?? throw new ArgumentNullException(nameof(cacheSettings));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void StartConsumer(string groupId, string topic, string facility, IConsumer<string, string> consumer, CancellationToken cancellationToken)
        {

            // get the Redis cache
            using var scope = _serviceScopeFactory.CreateScope();
            var _cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

            using (consumer)
            {
                consumer.Subscribe(topic);
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        
                        var consumeResult = consumer.Consume(cancellationToken);
                        // get the correlation id from the message and store it in Redis
                        string correlationId = string.Empty;
                        if (consumeResult.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                        {
                            correlationId = System.Text.Encoding.UTF8.GetString(headerValue);
                            string consumeResultFacility = this.extractFacility(consumeResult.Message.Key);
                   
                            if(facility != consumeResultFacility)
                            {
                                //  _logger.LogInformation("Searched Facility ID {facility} does not match message facility {consumeResultFacility}. Skipping message.", facility, consumeResultFacility);
                                continue;
                            }   
                            // read the list from Redis

                            var redisKey = topic + KafkaConsumerManager.delimitator + facility;
                            string retrievedListJson = _cache.GetString(redisKey);

                            var retrievedList = new List<string>();
                            if (retrievedListJson != null) {
                                retrievedList = JsonConvert.DeserializeObject<List<string>>(retrievedListJson);
                            }
                            // append the new correlation id to the existing list
                            if (!retrievedList.Contains(correlationId))
                            {
                                retrievedList.Add(correlationId);
                            }

                            string serializedList = JsonConvert.SerializeObject(retrievedList);

                            // store the list back in Redis
                        
                            _cache.SetString(redisKey, serializedList);

                        }
                        _logger.LogInformation("Consumed message '{MessageValue}' from topic {Topic}, partition {Partition}, offset {Offset}, correlation {CorrelationId}",consumeResult.Message.Value, consumeResult.Topic, consumeResult.Partition, consumeResult.Offset, correlationId);
                    }
                }
                catch (ConsumeException e)
                {
                    _logger.LogError(e, "Error occurred: {Reason}", e.Error.Reason);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Consumer {ConsumerName} stopped.", consumer.Name);
                    consumer.Dispose();
                }
                finally
                {
                    consumer.Close();
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
                   return  matchingProperty.Value.ToString();
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
