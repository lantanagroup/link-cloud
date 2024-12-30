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
    

        private readonly List<(IConsumer<string, string>, CancellationTokenSource)> _consumers;
        private readonly KafkaConnection _kafkaConnection;
        private readonly KafkaConsumerService _kafkaConsumerService;
        private readonly IOptions<CacheSettings> _cacheSettings;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        private readonly static string errorTopic = "-Error";
        public static readonly string delimitator = ":";

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
        public KafkaConsumerManager(KafkaConsumerService kafkaConsumerService, IOptions<Shared.Application.Models.Configs.CacheSettings> cacheSettings, IServiceScopeFactory serviceScopeFactory, KafkaConnection kafkaConnection)
        {
            _kafkaConsumerService = kafkaConsumerService;
            _cacheSettings = cacheSettings ?? throw new ArgumentNullException(nameof(cacheSettings));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _consumers = new List<(IConsumer<string, string>, CancellationTokenSource)>();
            _kafkaConnection = kafkaConnection ?? throw new ArgumentNullException(nameof(_kafkaConnection));
        }


        private IServiceScope ClearRedisCache(string facility)
        {
            // clear Redis cache
            var scope = _serviceScopeFactory.CreateScope();

            var _cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

            foreach (var topic in kafkaTopics)
            {
                {
                    String redisKey = topic.Item2 + delimitator + facility;
                    _cache.Remove(redisKey);
                }
            }
            return scope;
        }


        public void CreateAllConsumers(string facility)
        {
            //clear Redis cache for that facility
            ClearRedisCache(facility);

            // start consumers
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

            var consumer = new ConsumerBuilder<string, string>(config).Build();

            _consumers.Add((consumer, cts));

            Task.Run(() => _kafkaConsumerService.StartConsumer(groupId, topic, facility, consumer, cts.Token));
        }


        public Dictionary<string, string> readAllConsumers(string facility)
        {
            Dictionary<string, string> correlationIds = new Dictionary<string, string>();

            using var scope = _serviceScopeFactory.CreateScope();
            var _cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
            // loop through the redis keys for that facility and get the correlation id for each
            foreach (var topic in kafkaTopics)
            {
                if (topic.Item2 != string.Empty)
                {
                    string redisKey = topic.Item2 + delimitator + facility;

                    correlationIds.Add(topic.Item2, _cache.GetString(redisKey));
                }
            }
            return correlationIds;
        }

        public async Task StopAllConsumers(string facility)
        {
            // stop consumers
            foreach (var consumerTuple in _consumers)
            {
                consumerTuple.Item2.Cancel();
            }
            _consumers.Clear();

            //clear Redis cache for that facility
            ClearRedisCache(facility);

        }
    }


}
