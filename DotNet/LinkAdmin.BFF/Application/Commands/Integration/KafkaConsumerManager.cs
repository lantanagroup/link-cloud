using Hl7.Fhir.Utility;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{

    public class KafkaConsumerManager
    {
        private readonly List<Task> _consumerTasks = new List<Task>();
        private readonly KafkaConsumerService _kafkaConsumerService;
        private readonly IOptions<Shared.Application.Models.Configs.CacheSettings> _cacheSettings;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        // construct a list of topics 
        private List<(string, string)> kafkaTopics = new List<(string, string)>
          {
            ("ReportScheduledDyn", KafkaTopic.ReportScheduled.ToString()),
            ("", KafkaTopic.ReportScheduled.ToString()+"-Error"),
            ("CensusDyn", KafkaTopic.PatientIDsAcquired.ToString()),
            ("", KafkaTopic.PatientIDsAcquired.ToString()+"-Error"),
            ("QueryDispatchDyn", KafkaTopic.PatientEvent.ToString()),
            ("", KafkaTopic.PatientEvent.ToString()+"-Error"),
            ("DataAcquisitionDyn", KafkaTopic.DataAcquisitionRequested.ToString()),
            ("", KafkaTopic.DataAcquisitionRequested.ToString()+"-Error"),
            ("AcquiredDyn", KafkaTopic.ResourceAcquired.ToString()),
            ("", KafkaTopic.ResourceAcquired.ToString()+"-Error"),
            ("NormalizationDyn", KafkaTopic.ResourceNormalized.ToString()),
            ("", KafkaTopic.PatientNormalized.ToString()+"-Error"),
            ("ResourceEvaluatedDyn", KafkaTopic.ResourceEvaluated.ToString()),
            ("", KafkaTopic.ResourceEvaluated.ToString()+"-Error"),
            ("ReportDyn", KafkaTopic.SubmitReport.ToString()),
            ("", KafkaTopic.SubmitReport.ToString()+"-Error")
          };
            

        // Add constructor
        public KafkaConsumerManager(KafkaConsumerService kafkaConsumerService, IOptions<Shared.Application.Models.Configs.CacheSettings> cacheSettings, IServiceScopeFactory serviceScopeFactory)
        {
            _kafkaConsumerService = kafkaConsumerService;
            _cacheSettings = cacheSettings ?? throw new ArgumentNullException(nameof(cacheSettings));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        }

        public void CreateAllConsumers()
        {
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
            var consumerTask = Task.Run(() => _kafkaConsumerService.StartConsumer(groupId, topic));
            _consumerTasks.Add(consumerTask);
        }

        public void StopAllConsumers()
        {
            foreach (var task in _consumerTasks)
            {
                if (!task.IsCompleted)
                {
                    task.Dispose();
                }
            }
            _consumerTasks.Clear();
        }

        public Dictionary<string, string>  readAllConsumers()
        {
            Dictionary<string, string> correlationIds = new Dictionary<string, string>();
  
            if (_cacheSettings.Value.Enabled)
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var _cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
                // loop through the list of topics and get the correlation id for each
                foreach (var topic in kafkaTopics)
                {
                    if (topic.Item2 != "")
                    {
                        string key = topic.Item2 + " - CorrelationId";
                        correlationIds.Add(key, _cache.GetString(key));
                    }
                }
                return correlationIds;
            }
            return null;
        }
    }
}
