using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

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

        public void StartConsumer(string groupId, string topic)
        {
            var config = new ConsumerConfig
            {
                GroupId = groupId,
                BootstrapServers = "localhost:9092",  // Replace with your Kafka server address
                AutoOffsetReset = AutoOffsetReset.Earliest
            };

            using (var consumer = new ConsumerBuilder<Ignore, string>(config).Build())
            {
                consumer.Subscribe(topic);
                try
                {
                    while (true)
                    {
                        var consumeResult = consumer.Consume(CancellationToken.None);
                        // get the correlation id from the message and store it in Redis
                        string correlationId = string.Empty;
                        if (consumeResult.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                        {
                            correlationId = System.Text.Encoding.UTF8.GetString(headerValue);
                            if (_cacheSettings.Value.Enabled) { 
                                using var scope = _serviceScopeFactory.CreateScope();
                                var _cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
                                String key = topic + " - CorrelationId";
                                _cache.SetString(key, correlationId);
                            }
                        }
                        Console.WriteLine($"Consumed message '{consumeResult.Message.Value}' from topic {consumeResult.Topic}, partition {consumeResult.Partition}, offset {consumeResult.Offset}, correlation {correlationId}");
                    }
                }
                catch (ConsumeException e)
                {
                    Console.WriteLine($"Error occurred: {e.Error.Reason}");
                }
            }
        }
    }
}
