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


        public KafkaConsumerService(ICacheService cache, IServiceScopeFactory serviceScopeFactory,
            ILogger<KafkaConsumerService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache;
        }

        public void StartConsumer(string groupId, List<string> topics, string reportTrackingId,
            IConsumer<string, string> consumer, CancellationToken cancellationToken)
        {
            // get the cache
            using var scope = _serviceScopeFactory.CreateScope();

            using (consumer)
            {
                consumer.Subscribe(topics);
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var consumeResult = consumer.Consume(cancellationToken);

                        // Extract headers
                        string correlationId = string.Empty;
                        string traceId = string.Empty;
                        string errorMessage = null;

                        if (consumeResult.Message.Headers.TryGetLastBytes("X-Correlation-Id",
                                out var correlationHeader))
                        {
                            correlationId = System.Text.Encoding.UTF8.GetString(correlationHeader);

                            // read the exceptions
                            if (consumeResult.Message.Headers.TryGetLastBytes("X-Exception-Message",
                                    out var exceptionMessage))
                            {
                                errorMessage = System.Text.Encoding.UTF8.GetString(exceptionMessage);
                            }

                            else if (consumeResult.Message.Headers.TryGetLastBytes("X-Retry-Exception-Message",
                                         out var retryExceptionMessage))
                            {
                                errorMessage = System.Text.Encoding.UTF8.GetString(retryExceptionMessage);
                            }

                            else if (consumeResult.Message.Headers.TryGetLastBytes("kafka_exception-message",
                                         out var kafkaErrorBytes))
                            {
                                errorMessage = System.Text.Encoding.UTF8.GetString(kafkaErrorBytes);
                            }

                            // Extract traceId from traceparent header
                            if (consumeResult.Message.Headers.TryGetLastBytes("traceparent", out var traceParentBytes))
                            {
                                string traceParent = System.Text.Encoding.UTF8.GetString(traceParentBytes);

                                // Split by '-' and get the second part (traceId)
                                string[] parts = traceParent.Split('-');
                                if (parts.Length >= 2)
                                {
                                    traceId = parts[1];
                                }
                            }

                            if (!checkReportTrackingId(consumeResult.Message.Value, reportTrackingId) &&
                                !checkReportTrackingId(consumeResult.Message.Key, reportTrackingId))
                            {
                                continue;
                            }

                            string patientId = getPatientId(consumeResult.Message.Value);
                            
                            // read the list from cache
                            string topicName = consumeResult.Topic;
                            var cacheKey = topicName + KafkaConsumerManager.delimiter + reportTrackingId;

                            string retrievedListJson;
                            try
                            {
                                retrievedListJson = _cache.Get<string>(cacheKey);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to retrieve correlation IDs from cache for key {key}",
                                    HtmlInputSanitizer.Sanitize(cacheKey));
                                retrievedListJson = null;
                            }

                            var retrievedList = string.IsNullOrEmpty(retrievedListJson)
                                ? new List<CorrelationCacheEntry>()
                                : JsonConvert.DeserializeObject<List<CorrelationCacheEntry>>(retrievedListJson);

                            // Add new entry if not present
                            var existingEntry = retrievedList.FirstOrDefault(x => x.CorrelationId == correlationId);
                            if (existingEntry == null)
                            {
                                // Only store TraceId and ErrorMessage if there is an error
                                retrievedList.Add(new CorrelationCacheEntry
                                {
                                    CorrelationId = correlationId,
                                    PatientId = patientId,
                                    ErrorMessage = !string.IsNullOrEmpty(errorMessage) ? errorMessage : null,
                                    TraceId = !string.IsNullOrEmpty(errorMessage) ? traceId : null
                                });
                            }
                            else
                            {
                                // Update only if there is a new error
                                if (!string.IsNullOrEmpty(errorMessage))
                                {
                                    existingEntry.ErrorMessage = errorMessage;
                                    existingEntry.TraceId = traceId;
                                }
                            }

                            // Save updated list to Redis
                            _cache.Set(cacheKey, JsonConvert.SerializeObject(retrievedList), TimeSpan.FromMinutes(30));
                        }
                    }
                }
                catch (ConsumeException e)
                {
                    if (e.ConsumerRecord != null)
                    {
                        _logger.LogError(e,
                            "Error occurred during consumption. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}, Reason: {Reason}",
                            e.ConsumerRecord.Topic, e.ConsumerRecord.Partition.Value, e.ConsumerRecord.Offset.Value,
                            e.Error.Reason);
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

        private string getPatientId(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;

            try
            {
                var jsonObject = JObject.Parse(input);
                return jsonObject.Descendants()
                    .OfType<JProperty>()
                    .FirstOrDefault(p => p.Name.Equals("PatientId", StringComparison.OrdinalIgnoreCase))
                    ?.Value.ToString();
            }
            catch (JsonException ex)
             { 
                 _logger.LogWarning(ex, "Failed to parse message body for PatientId"); 
                 return null;
             }
        }
    }
}

public class CorrelationCacheEntry
{
    public string CorrelationId { get; set; }
    
    public string? PatientId { get; set; }
    public string ErrorMessage { get; set; }
    public string TraceId { get; set; }
}