using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

        public void StartConsumer(string groupId, string topic, string reportTrackingId, IConsumer<string, string> consumer, CancellationToken cancellationToken)
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
                        string errorMessage = null;

                        var consumeResult = consumer.Consume(cancellationToken);
                        // get the correlation id from the message and store it in Cache
                        string correlationId = string.Empty;

                        if (consumeResult.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                        {

                            if (consumeResult.Message.Headers.TryGetLastBytes("X-Exception-Message", out var exceptionMessage))
                            {
                                errorMessage = System.Text.Encoding.UTF8.GetString(exceptionMessage);
                            }

                            if (consumeResult.Message.Headers.TryGetLastBytes("X-Retry-Exception-Message", out var retryExceptionMessage))
                            {
                                errorMessage = System.Text.Encoding.UTF8.GetString(retryExceptionMessage);
                            }

                            else if (consumeResult.Message.Headers.TryGetLastBytes("kafka_exception-message", out var kafkaErrorBytes))
                            {
                                errorMessage = System.Text.Encoding.UTF8.GetString(kafkaErrorBytes);
                            }

                            correlationId = System.Text.Encoding.UTF8.GetString(headerValue);
                            if (!checkReportTrackingId(consumeResult.Message.Value, reportTrackingId) && !checkReportTrackingId(consumeResult.Message.Key, reportTrackingId))
                            {
                                continue;
                            }

                            // read the list from cache
                            var cacheKey = topic + KafkaConsumerManager.delimiter + reportTrackingId;

                            string retrievedListJson;
                            try
                            {
                                retrievedListJson = _cache.Get<string>(cacheKey);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to retrieve correlation IDs from cache for key {key}", HtmlInputSanitizer.Sanitize(cacheKey));
                                retrievedListJson = null;
                            }

                            var retrievedList = string.IsNullOrEmpty(retrievedListJson) ? new List<CorrelationCacheEntry>() : JsonConvert.DeserializeObject<List<CorrelationCacheEntry>>(retrievedListJson);

                            // Add new entry if not present
                            var existingEntry = retrievedList.FirstOrDefault(x => x.CorrelationId == correlationId);
                            if (existingEntry == null)
                            {
                                retrievedList.Add(new CorrelationCacheEntry
                                {
                                    CorrelationId = correlationId,
                                    ErrorMessage = errorMessage
                                });
                            }
                            else if (!string.IsNullOrEmpty(errorMessage))
                            {
                                // Update error message if new one is present
                                existingEntry.ErrorMessage = errorMessage;
                            }

                            // Save updated list to Redis
                            _cache.Set(cacheKey, JsonConvert.SerializeObject(retrievedList), TimeSpan.FromMinutes(30));

                        }
                        // _logger.LogInformation("Consumed message '{MessageValue}' from topic {Topic}, partition {Partition}, offset {Offset}, correlation {CorrelationId}", consumeResult.Message.Value, consumeResult.Topic, consumeResult.Partition, consumeResult.Offset, correlationId);
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

        private bool checkReportTrackingId(string input, string reportTrackingId)
        {
            if (string.IsNullOrEmpty(input)) return false;

            try
            {
                var jsonObject = JObject.Parse(input);
                var allIds = jsonObject.Descendants()
                            .OfType<JProperty>()
                            .Where(p => p.Name.Equals("ReportTrackingId", StringComparison.OrdinalIgnoreCase)
                                     || p.Name.Equals("ReportScheduleId", StringComparison.OrdinalIgnoreCase))
                            .Select(p => p.Value.ToString())
                            .ToList();
                return allIds.Contains(reportTrackingId);
            }
            catch
            {
                return false;
            }
        }
    }

}

public class CorrelationCacheEntry
{
    public string CorrelationId { get; set; }
    public string ErrorMessage { get; set; }
}


