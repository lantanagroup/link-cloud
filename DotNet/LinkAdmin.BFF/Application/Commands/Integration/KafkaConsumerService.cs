using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;


namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{
    public class KafkaConsumerService
    {
        private readonly IOptions<CacheSettings> _cacheSettings;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public KafkaConsumerService( IOptions<CacheSettings> cacheSettings, IServiceScopeFactory serviceScopeFactory)
        {
            _cacheSettings = cacheSettings ?? throw new ArgumentNullException(nameof(cacheSettings));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        }

        public void StartConsumer(string groupId, string topic, IConsumer<Ignore, string> consumer, CancellationToken cancellationToken)
        {
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
                            using var scope = _serviceScopeFactory.CreateScope();
                            var _cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
                            // append the new correlation id to the existing list
                            var retrievedList = new List<string>();
                            string retrievedListJson = _cache.GetString(topic);
                            if (retrievedListJson != null) {
                                retrievedList = JsonConvert.DeserializeObject<List<string>>(retrievedListJson);
                            }
                            if (!retrievedList.Contains(correlationId))
                            {
                                retrievedList.Add(correlationId);
                            }

                            string serializedList = JsonConvert.SerializeObject(retrievedList);

                            _cache.SetString(topic, serializedList);
                        }
                        Console.WriteLine($"Consumed message '{consumeResult.Message.Value}' from topic {consumeResult.Topic}, partition {consumeResult.Partition}, offset {consumeResult.Offset}, correlation {correlationId}");
                    }
                }
                catch (ConsumeException e)
                {
                    Console.WriteLine($"Error occurred: {e.Error.Reason}");
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"Consumer {consumer.Name} stopped.");
                    consumer.Dispose();
                }
                finally
                {
                    consumer.Close();
                }
            }
        }
    }
}
