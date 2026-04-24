using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Application.Models.Cache;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using LantanaGroup.Link.Normalization.Application.Settings;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.OpenApi.Writers;
using OpenTelemetry.Resources;
using System.Text;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.Shared.Application.Enums;
using StackExchange.Redis;
using LantanaGroup.Link.Shared.Application.Extensions.Caching;

namespace LantanaGroup.Link.Normalization.Listeners;

public class ResourcesAcquiredListener : BackgroundService
{
    private readonly ILogger<ResourcesAcquiredListener> _logger;
    private readonly IKafkaConsumerFactory<ResourceKey, ResourcesAcquiredValue> _consumerFactory;
    private readonly IProducer<ResourceKey, ResourcesNormalizedMessage> _producer;
    private readonly IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, string> _consumeExceptionHandler;
    private readonly IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue> _deadLetterExceptionHandler;
    private readonly ITransientExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue> _transientExceptionHandler;
    private bool _cancelled = false;
    private readonly INormalizationServiceMetrics _metrics;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ServiceInformation _serviceInformation;

    private readonly CopyPropertyOperationService _copyPropertyOperationService;
    private readonly CodeMapOperationService _codeMapOperationService;
    private readonly ConditionalTransformOperationService _conditionalTransformOperationService;
    private readonly CopyLocationOperationService _copyLocationOperationService;
    private readonly IResourceCache _redisCache;

    public ResourcesAcquiredListener(
        ILogger<ResourcesAcquiredListener> logger,
        ServiceInformation serviceInformation,
        IServiceScopeFactory scopeFactory,
        IKafkaConsumerFactory<ResourceKey, ResourcesAcquiredValue> consumerFactory,
        IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, string> consumeExceptionHandler,
        IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue> deadLetterExceptionHandler,
        ITransientExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue> transientExceptionHandler,
        INormalizationServiceMetrics metrics,
        IProducer<ResourceKey, ResourcesNormalizedMessage> producer,
        CopyPropertyOperationService copyPropertyOperationService,
        CodeMapOperationService codeMapOperationService,
        ConditionalTransformOperationService conditionalTransformOperationService,
        CopyLocationOperationService copyLocationOperationService, 
        IResourceCache redisCache)
    {
        this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _consumerFactory = consumerFactory ?? throw new ArgumentNullException(nameof(consumerFactory));
        _consumeExceptionHandler = consumeExceptionHandler ?? throw new ArgumentNullException(nameof(consumeExceptionHandler));

        _consumeExceptionHandler.Topic = $"{nameof(KafkaTopic.ResourcesAcquired)}-Error";
        _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentNullException(nameof(deadLetterExceptionHandler));

        _deadLetterExceptionHandler.Topic = $"{nameof(KafkaTopic.ResourcesAcquired)}-Error";
        _transientExceptionHandler = transientExceptionHandler;

        _transientExceptionHandler.Topic = KafkaTopic.ResourcesAcquiredRetry.GetStringValue();
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));

        _scopeFactory = scopeFactory;
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));

        _serviceInformation = serviceInformation ?? throw new ArgumentNullException(nameof(serviceInformation));

        _copyPropertyOperationService = copyPropertyOperationService;
        _codeMapOperationService = codeMapOperationService ?? throw new ArgumentNullException(nameof(codeMapOperationService));
        _conditionalTransformOperationService = conditionalTransformOperationService ?? throw new ArgumentNullException(nameof(conditionalTransformOperationService));
        _copyLocationOperationService = copyLocationOperationService ?? throw new ArgumentNullException(nameof(copyLocationOperationService));
        _redisCache = redisCache ?? throw new ArgumentNullException(nameof(redisCache));
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() => StartConsumerLoop(cancellationToken), cancellationToken);
    }

    private async Task StartConsumerLoop(CancellationToken cancellationToken)
    {
        using var kafkaConsumer = _consumerFactory.CreateConsumer(new ConsumerConfig
        {
            GroupId = _serviceInformation.ServiceConfigName,
            EnableAutoCommit = false
        });

        kafkaConsumer.Subscribe(new string[] { KafkaTopic.ResourcesAcquired.ToString() });

        while (!cancellationToken.IsCancellationRequested && !_cancelled)
        {
            ConsumeResult<ResourceKey, ResourcesAcquiredValue>? message = default;
            try
            {
                await kafkaConsumer.ConsumeWithInstrumentation(async (result, CancellationToken) =>
                {
                    try
                    {
                        message = result;

                        if (message == null || message.Message == null) {
                            throw new DeadLetterException("Event is null");
                        }

                        if (message.Message.Key == null || string.IsNullOrWhiteSpace(message.Message.Key.FacilityId) || string.IsNullOrWhiteSpace(message.Message.Key.PatientId))
                        {
                            throw new DeadLetterException("Malformed key in the event. Facility Id and Patient Id are required.");
                        }

                        //TODO - Daniel: Add message value handling. But first check if there is something that can be done on the serdes level rather than having to do this type of checking accross all values

                        string correlationId;

                        try
                        {
                            correlationId = ExtractCorrelationId(message.Message);
                        }
                        catch (Exception ex)
                        {
                            throw new DeadLetterException("Failed to extract FacilityId and CorrelationId from message.", ex);
                        }

                        if (string.IsNullOrWhiteSpace(message.Message.Value.ReportableEvent))
                        {
                            throw new DeadLetterException("ReportableEvent value is null or empty");
                        }

                        using (var scope = _scopeFactory.CreateScope())
                        {
                            //IResourceCache resourceCache;

                            ////TODO: Daniel - Can we inject these classes?
                            //switch (message.Message.Value.CacheType)
                            //{
                            //    case ResourceCacheType.ABS: resourceCache = _redisResourceCache; break; //TODO: Daniel - Implement ABS class
                            //    case ResourceCacheType.Redis: resourceCache = _redisResourceCache; break;
                            //    default:
                            //        throw new DeadLetterException($"Cache Type '{message.Message.Value.CacheType.ToString()}' not supported");
                            //}

                            foreach (var cacheKey in message.Message.Value.CacheKeys)
                            {
                                //ResourceType resourceType = resourceCache.GetResourceTypeByEventKey(cacheKey);
                                ResourceType resourceType = GetResourceTypeByEventKey(cacheKey, message.Message.Value.CacheType);

                                var operationSequenceQueries = scope.ServiceProvider.GetRequiredService<IOperationSequenceQueries>();

                                var sequences = await operationSequenceQueries.Search(new OperationSequenceSearchModel()
                                {
                                    FacilityId = message.Message.Key.FacilityId,
                                    ResourceType = resourceType.ToString(),
                                });

                                if (sequences == null || sequences.Count == 0) {
                                    continue;
                                }

                                List<DomainResource> resources = _redisCache.Get(cacheKey);
                                //List <DomainResource> resources = _redisCache.Get<List<DomainResource>>(cacheKey);


                                foreach (var resource in resources) {
                                    sequences.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));

                                    foreach (var sequence in sequences)
                                    {
                                        var dbEntity = sequence.OperationResourceType.Operation;

                                        var operation = OperationHelper.GetOperation(dbEntity.OperationType, dbEntity.OperationJson);

                                        if (operation == null)
                                        {
                                            throw new TransientException("Operation Data Entity found, but the operation failed to deserialize");
                                        }

                                        var operationResult = operation.OperationType switch
                                        {
                                            OperationType.CopyProperty => await _copyPropertyOperationService.ProcessOperationAsync((CopyPropertyOperation)operation, resource),
                                            OperationType.CodeMap => await _codeMapOperationService.ProcessOperationAsync((CodeMapOperation)operation, resource),
                                            OperationType.ConditionalTransform => await _conditionalTransformOperationService.ProcessOperationAsync((ConditionalTransformOperation)operation, resource),
                                            OperationType.CopyLocation => await _copyLocationOperationService.ProcessOperationAsync((CopyLocationOperation)operation, resource),
                                            _ => null
                                        };

                                        if (operationResult != null && operationResult.SuccessCode != OperationStatus.Failure)
                                        {
                                            _metrics.IncrementResourceNormalizedCounter(new List<KeyValuePair<string, object?>>() {
                                                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, message.Message.Key.FacilityId),
                                                new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, correlationId),
                                                new KeyValuePair<string, object?>(DiagnosticNames.PatientId, message.Message.Key.PatientId),
                                                new KeyValuePair<string, object?>(DiagnosticNames.Resource, resource.TypeName),
                                                new KeyValuePair<string, object?>(DiagnosticNames.QueryType, message.Message.Value.QueryType),
                                                new KeyValuePair<string, object?>(DiagnosticNames.NormalizationOperation, operation.OperationType.ToString())});
                                        }
                                        else
                                        {
                                            _logger.LogWarning("Normalization Operation Failed ({FacilityId}, {CorrelationId}, {OperationType}): {ErrorMessage}", message.Message.Key.FacilityId, correlationId, operation.OperationType, operationResult?.ErrorMessage ?? "No Operation Result Error Message");
                                        }
                                    }
                                }

                               
                                
                                //try
                                //{
                                //    resource = DeserializeResource(message.Message.Value.Resource);
                                //}
                                //catch (Exception ex)
                                //{
                                //    if (ex is JsonException || ex is NotSupportedException)
                                //    {
                                //        throw new TransientException("Failed to deserialize resource.", ex);
                                //    }

                                //    throw new DeadLetterException("Failed to deserialize resource.", ex);
                                //}

                                
                                
                            }
                                
                            await ProduceResourcesNormalizedMessage(message, message.Message.Key.FacilityId, correlationId);
                            
                        }
                    }
                    catch (DeadLetterException ex)
                    {
                        _deadLetterExceptionHandler.HandleException(message, ex, message.Key?.FacilityId ?? string.Empty);
                    }
                    catch (TransientException ex)
                    {
                        _transientExceptionHandler.HandleException(message, ex, message.Key?.FacilityId ?? string.Empty);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to process Patient Event.");

                        _transientExceptionHandler.HandleException(message, new TransientException("Normalization Exception thrown: " + ex.Message, ex), message.Key?.FacilityId ?? string.Empty);
                    }
                    finally
                    {
                        kafkaConsumer.Commit(message);
                    }
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                continue;
            }
            catch (ConsumeException ex)
            {
                if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                {
                    throw new OperationCanceledException(ex.Error.Reason, ex);
                }

                string facilityId = string.Empty;
                if (ex.ConsumerRecord?.Message?.Key != null)
                {
                    try
                    {
                        var key = JsonSerializer.Deserialize<ResourceKey>(ex.ConsumerRecord.Message.Key);
                        facilityId = key?.FacilityId ?? string.Empty;
                    }
                    catch
                    {
                        // ignore
                    }
                }

                _consumeExceptionHandler.HandleConsumeException(ex, facilityId);
                TopicPartitionOffset? offset = ex.ConsumerRecord?.TopicPartitionOffset;
                if (offset == null)
                {
                    kafkaConsumer.Commit();
                }
                else
                {
                    kafkaConsumer.Commit(new List<TopicPartitionOffset> {
                        offset
                    });
                }
                continue;
            }
        }
    }


    private async Task ProduceResourcesNormalizedMessage(ConsumeResult<ResourceKey, ResourcesAcquiredValue>? message, string facilityId, string correlationId)
    {
        var headers = new Headers
        {
            new Header(NormalizationConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(correlationId))
        };

        var resourceNormalizedMessage = new ResourcesNormalizedMessage
        {
            QueryType = message.Message.Value.QueryType,
            ScheduledReports = message.Message.Value.ScheduledReports,
            ReportableEvent = message.Message.Value.ReportableEvent,
            CacheType = message.Message.Value.CacheType,
            CacheKeys = message.Message.Value.CacheKeys
        };
        Message<ResourceKey, ResourcesNormalizedMessage> produceMessage = new Message<ResourceKey, ResourcesNormalizedMessage>
        {
            Key = message.Message.Key,
            Headers = headers,
            Value = resourceNormalizedMessage
        };

        try
        {
            await _producer.ProduceAsync(KafkaTopic.ResourcesNormalized.ToString(), produceMessage);
        }
        catch (ProduceException<ResourceKey, ResourcesNormalizedMessage> ex)
        {
            _logger.LogError(ex, "Failed to produce ResourcesNormalized message. FacilityId: {FacilityId}, CorrelationId: {CorrelationId}, ResourceAcquired Partition: {Partition}, ResourceAcquired Offset: {Offset}", facilityId, correlationId, message.Partition.Value, message.Offset.Value);
            throw new TransientException($"Failed to produce ResourcesNormalized message: {ex.Message}", ex);
        }
    }

    public void Cancel()
    {
        this._cancelled = true;
    }

    private string ExtractCorrelationId(Message<ResourceKey, ResourcesAcquiredValue> message)
    {
        var cIBytes = message.Headers.FirstOrDefault(x => x.Key == NormalizationConstants.HeaderNames.CorrelationId)?.GetValueBytes();

        if (cIBytes == null || cIBytes.Length == 0)
            throw new MissingCorrelationIdException();

        var correlationId = Encoding.UTF8.GetString(cIBytes);

        return correlationId;
    }

    private DomainResource DeserializeStringToResource(string json)
    {
        return JsonSerializer.Deserialize<DomainResource>(json, LinkFhirSerializerOptions.ForFhirLenientSerialization);
    }

    private DomainResource DeserializeResource(object resource)
    {

        switch (resource)
        {
            case JsonElement:
                return DeserializeStringToResource(resource.ToString());
            case string:
                return DeserializeStringToResource((string)resource);
            default:
                throw new DeserializationUnsupportedTypeException();
        }
    }

    private ResourceType GetResourceTypeByEventKey(string cacheKey, ResourceCacheType cacheType)
    {
        if (cacheType == ResourceCacheType.Redis)
        {
            string[] splitKey = cacheKey.Split(":");

            if (splitKey.Length != 2)
            {
                throw new Exception($"Cache key '{cacheKey}' does not contain required ':' divider. Expected format is <correlation id>:<resource type>");
            }

            if (Enum.TryParse<ResourceType>(splitKey[1], out var resourceType))
            {
                return resourceType;
            }
            else
            {
                throw new Exception($"Could not parse the Redis cache key '{cacheKey}' into a valid FHIR Resource Type");
            }
        }

        //TODO: Daniel - Add ABS
        return ResourceType.Encounter;
    }

}
