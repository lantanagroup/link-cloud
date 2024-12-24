using Confluent.Kafka;

using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Caching.Distributed;

using Microsoft.Extensions.Options;

using static Confluent.Kafka.ConfigPropertyNames;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{

    public class KafkaConsumerManager
    {
 
        private readonly List<(IConsumer<Ignore, string>, CancellationTokenSource)> _consumers;
        private readonly KafkaConnection _kafkaConnection;
        private readonly KafkaConsumerService _kafkaConsumerService;
        private readonly IOptions<CacheSettings> _cacheSettings;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        // construct a list of topics 
        private List<(string, string)> kafkaTopics = new List<(string, string)>
          {
            ("ReportScheduledDyn", KafkaTopic.ReportScheduled.ToString()),
            ("ReportScheduledDyn", KafkaTopic.ReportScheduled.ToString()+"-Error"),
            ("CensusDyn", KafkaTopic.PatientIDsAcquired.ToString()),
            ("CensusDyn", KafkaTopic.PatientIDsAcquired.ToString()+"-Error"),
            ("QueryDispatchDyn", KafkaTopic.PatientEvent.ToString()),
            ("QueryDispatchDyn", KafkaTopic.PatientEvent.ToString()+"-Error"),
            ("DataAcquisitionDyn", KafkaTopic.DataAcquisitionRequested.ToString()),
            ("DataAcquisitionDyn", KafkaTopic.DataAcquisitionRequested.ToString()+"-Error"),
            ("AcquiredDyn", KafkaTopic.ResourceAcquired.ToString()),
            ("AcquiredDyn", KafkaTopic.ResourceAcquired.ToString()+"-Error"),
            ("NormalizationDyn", KafkaTopic.ResourceNormalized.ToString()),
            ("NormalizationDyn", KafkaTopic.ResourceNormalized.ToString()+"-Error"),
             ("ReportDyn", KafkaTopic.SubmitReport.ToString()),
            ("ReportDyn", KafkaTopic.SubmitReport.ToString()+"-Error"),
            ("ResourceEvaluatedDyn", KafkaTopic.ResourceEvaluated.ToString()),
            ("ResourceEvaluatedDyn", KafkaTopic.ResourceEvaluated.ToString()+"-Error"),        
          };


        // Add constructor
        public KafkaConsumerManager(KafkaConsumerService kafkaConsumerService, IOptions<Shared.Application.Models.Configs.CacheSettings> cacheSettings, IServiceScopeFactory serviceScopeFactory, KafkaConnection kafkaConnection)
        {
            _kafkaConsumerService = kafkaConsumerService;
            _cacheSettings = cacheSettings ?? throw new ArgumentNullException(nameof(cacheSettings));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _consumers = new List<(IConsumer<Ignore, string>, CancellationTokenSource)>();
            _kafkaConnection = kafkaConnection ?? throw new ArgumentNullException(nameof(_kafkaConnection));
        }

        public void CreateAllConsumers()
        {
            // clear Redis cache
            using var scope = _serviceScopeFactory.CreateScope();

            var _cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

            foreach (var topic in kafkaTopics)
            {             
                {
                    String key = topic.Item2;
                    _cache.Remove(key);
                }
            }

            // loop through the list of topics and create a consumer for each
            foreach (var topic in kafkaTopics)
            {
                if (topic.Item1 != "")
                {
                    CreateConsumer(topic.Item1, topic.Item2);
                }
              
            }
          
        }

        public void CreateConsumer(string groupId, string topic)
        {
            var cts = new CancellationTokenSource();
            var config = new ConsumerConfig
            {
                GroupId = groupId,
                BootstrapServers = string.Join(", ", _kafkaConnection.BootstrapServers),
                AutoOffsetReset = AutoOffsetReset.Earliest
            };

            if (_kafkaConnection.SaslProtocolEnabled)
            {
                config.SecurityProtocol = SecurityProtocol.SaslPlaintext;
                config.SaslMechanism = SaslMechanism.Plain;
                config.SaslUsername = _kafkaConnection.SaslUsername;
                config.SaslPassword = _kafkaConnection.SaslPassword;
            }

            var consumer = new ConsumerBuilder<Ignore, string>(config).Build();

            _consumers.Add((consumer, cts));

            Task.Run(() => _kafkaConsumerService.StartConsumer(groupId, topic, consumer, cts.Token));
        }

        public async Task StopAllConsumers()
        {

            foreach (var consumerTuple in _consumers)
            {
                consumerTuple.Item2.Cancel();
            }

            _consumers.Clear();

        }

        public Dictionary<string, string> readAllConsumers()
        {
            Dictionary<string, string> correlationIds = new Dictionary<string, string>();

            using var scope = _serviceScopeFactory.CreateScope();
            var _cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
            // loop through the list of topics and get the correlation id for each
            foreach (var topic in kafkaTopics)
            {
                if (topic.Item2 != "")
                {
                    string key = topic.Item2;

                    correlationIds.Add(key, _cache.GetString(key));

                }
            }
            return correlationIds;
        }
    }
}
