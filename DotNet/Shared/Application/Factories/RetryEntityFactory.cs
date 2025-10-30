using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Settings;
using System.Text;
using System.Text.RegularExpressions;

namespace LantanaGroup.Link.Shared.Application.Factories
{
    public partial class RetryModelFactory : IRetryModelFactory
    {
        [GeneratedRegex(@"-Retry$", RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex RetrySuffix();

        public RetryModel CreateRetryModel(ConsumeResult<string, string> consumeResult, ConsumerSettings consumerSettings) 
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            foreach (var header in consumeResult.Message.Headers)
            {
                headers.TryAdd(header.Key, Encoding.UTF8.GetString(header.GetValueBytes()));
            }

            int retryCount = 1;

            if (headers.ContainsKey(KafkaConstants.HeaderConstants.RetryCount))
            {
                int count = int.Parse(headers[KafkaConstants.HeaderConstants.RetryCount]);
                retryCount = count += 1;
                headers[KafkaConstants.HeaderConstants.RetryCount] = retryCount.ToString();
            }
            else
            {
                headers.TryAdd(KafkaConstants.HeaderConstants.RetryCount, retryCount.ToString());
            }

            var triggerDuration = System.Xml.XmlConvert.ToTimeSpan(consumerSettings.ConsumerRetryDuration[retryCount - 1]);
            var triggerDate = DateTime.Now.Add(triggerDuration);

            RetryModel retryEntity = new RetryModel
            {
                ServiceName = headers.FirstOrDefault(x => x.Key == KafkaConstants.HeaderConstants.ExceptionService).Value ?? "",
                FacilityId = headers.FirstOrDefault(x => x.Key == KafkaConstants.HeaderConstants.ExceptionFacilityId).Value ?? "",
                ScheduledTrigger = triggerDate,
                Topic = RetrySuffix().Replace(consumeResult.Topic, ""),
                Key = consumeResult.Message.Key,
                Value = consumeResult.Message.Value,
                RetryCount = retryCount,
                CorrelationId = headers.FirstOrDefault(x => x.Key == KafkaConstants.HeaderConstants.CorrelationId).Value ?? "",
                Headers = headers
            };
            
            return retryEntity;
        }
    }
}
