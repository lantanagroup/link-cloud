using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using LantanaGroup.Link.Normalization.Application.Settings;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using System.Text;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Normalization.Listeners;

public class ResourcesAcquiredListener : BackgroundService
{
    private readonly ILogger<ResourcesAcquiredListener> _logger;
    private readonly IKafkaConsumerFactory<ResourceKey, ResourcesAcquiredValue> _consumerFactory;
    private readonly IProducer<ResourceKey, ResourcesNormalizedValue> _producer;
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
    private readonly CopyLocationAliasToTypeIterativelyOperationService _copyLocationAliasToTypeIterativelyOperationService;
    private readonly RemoveExtensionsOperationService _removeExtensionsOperationService;
    private readonly IResourceCache _resourceCache;

    public ResourcesAcquiredListener(
        ILogger<ResourcesAcquiredListener> logger,
        ServiceInformation serviceInformation,
        IServiceScopeFactory scopeFactory,
        IKafkaConsumerFactory<ResourceKey, ResourcesAcquiredValue> consumerFactory,
        IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, string> consumeExceptionHandler,
        IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue> deadLetterExceptionHandler,
        ITransientExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue> transientExceptionHandler,
        INormalizationServiceMetrics metrics,
        IProducer<ResourceKey, ResourcesNormalizedValue> producer,
        CopyPropertyOperationService copyPropertyOperationService,
        CodeMapOperationService codeMapOperationService,
        ConditionalTransformOperationService conditionalTransformOperationService,
        CopyLocationOperationService copyLocationOperationService,
        CopyLocationAliasToTypeIterativelyOperationService copyLocationAliasToTypeIterativelyOperationService,
        RemoveExtensionsOperationService removeExtensionsOperationService,
        IResourceCache resourceCache)
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
        _copyLocationAliasToTypeIterativelyOperationService = copyLocationAliasToTypeIterativelyOperationService ?? throw new ArgumentNullException(nameof(copyLocationAliasToTypeIterativelyOperationService));
        _removeExtensionsOperationService = removeExtensionsOperationService ?? throw new ArgumentNullException(nameof(removeExtensionsOperationService));
        _resourceCache = resourceCache ?? throw new ArgumentNullException(nameof(resourceCache));
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
            try
            {
                await kafkaConsumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                {
                    try
                    {
                        await ProcessMessageAsync(result,  consumeCancellationToken);
                    }
                    catch (DeadLetterException ex)
                    {
                        _deadLetterExceptionHandler.HandleException(result, ex, result.Message.Key?.FacilityId ?? string.Empty);
                    }
                    catch (TransientException ex)
                    {
                        _transientExceptionHandler.HandleException(result, ex, result.Message.Key?.FacilityId ?? string.Empty);
                    }
                    catch (OperationCanceledException) when (consumeCancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process ResourceAcquired event for facility {FacilityId}.", result?.Message.Key?.FacilityId?.SanitizeForLog());

                        _transientExceptionHandler.HandleException(result, new TransientException("Normalization Exception thrown: " + ex.Message, ex), result.Message.Key?.FacilityId ?? string.Empty);
                    }
                    finally
                    {
                        if (!consumeCancellationToken.IsCancellationRequested)
                            kafkaConsumer.Commit(result);
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

    public async Task ProcessMessageAsync(ConsumeResult<ResourceKey, ResourcesAcquiredValue> result, CancellationToken cancellationToken)
    {
        ValidateResourcesAcquiredEvent(result, out string correlationId);

        IResourceCache resourceCache = _resourceCache.GetImplementation(result.Message.Value.CacheType);

        using (var scope = _scopeFactory.CreateScope())
        {
            foreach (var cacheKey in result.Message.Value.CacheKeys)
            {
                ResourceType resourceType = resourceCache.GetResourceTypeByCacheKey(cacheKey);

                var operationSequenceQueries = scope.ServiceProvider.GetRequiredService<IOperationSequenceQueries>();

                var sequences = await operationSequenceQueries.Search(new OperationSequenceSearchModel()
                {
                    FacilityId = result.Message.Key.FacilityId,
                    ResourceType = resourceType.ToString()
                }, cancellationToken: cancellationToken);

                List<DomainResource> resources = resourceCache.Get(cacheKey);

                if (sequences == null || sequences.Count == 0)
                {
                    _logger.LogDebug("No operation sequences configured for {FacilityId}/{ResourceType}. Passing resource through without normalization.", result.Message.Key.FacilityId.SanitizeForLog(), resourceType.ToString().SanitizeForLog());
                }
                else
                {
                    sequences.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
                    foreach (var resource in resources)
                    {
                        foreach (var sequence in sequences)
                        {
                            var dbEntity = sequence.OperationResourceType.Operation;
                            if(dbEntity != null && dbEntity.IsDisabled)
                            {
                                _logger.LogDebug("Skipping disabled operation {OperationType} ({OperationName}) for {FacilityId}/{ResourceType}/{ResourceId}.", dbEntity.OperationType, dbEntity.Name.SanitizeForLog(), result.Message.Key.FacilityId.SanitizeForLog(), resource.TypeName.SanitizeForLog(), resource.Id.SanitizeForLog());
                                continue;
                            }

                            var operation = OperationHelper.GetOperation(dbEntity.OperationType, dbEntity.OperationJson);
                            if (operation == null)
                            {
                                throw new TransientException("Operation Data Entity found, but the operation failed to deserialize");
                            }

                            _logger.LogInformation("Normalizing {ResourceType}/{ResourceId} with {OperationType} operation ({OperationName})", resource.TypeName.SanitizeForLog(), resource.Id.SanitizeForLog(), operation.OperationType, operation.Name.SanitizeForLog());

                        var operationResult = operation.OperationType switch
                        {
                            OperationType.CopyProperty => await _copyPropertyOperationService.ProcessOperationAsync((CopyPropertyOperation)operation, resource, cancellationToken: cancellationToken),
                            OperationType.CodeMap => await _codeMapOperationService.ProcessOperationAsync((CodeMapOperation)operation, resource, cancellationToken: cancellationToken),
                            OperationType.ConditionalTransform => await _conditionalTransformOperationService.ProcessOperationAsync((ConditionalTransformOperation)operation, resource, cancellationToken: cancellationToken),
                            OperationType.CopyLocation => await _copyLocationOperationService.ProcessOperationAsync((CopyLocationOperation)operation, resource, cancellationToken: cancellationToken),
                            OperationType.RemoveExtensions => await _removeExtensionsOperationService.ProcessOperationAsync((RemoveExtensionsOperation)operation, resource, cancellationToken: cancellationToken),
                            OperationType.CopyLocationAliasToTypeIteratively => await _copyLocationAliasToTypeIterativelyOperationService.ProcessOperationAsync((CopyLocationAliasToTypeIterativelyOperation)operation, resource, resources.OfType<Location>().ToList<DomainResource>(), cancellationToken),
                            _ => null
                        };

                            if (operationResult != null && operationResult.SuccessCode != OperationStatus.Failure)
                            {
                                if (operationResult.SuccessCode == OperationStatus.Success)
                                {
                                    _logger.LogInformation("Changed {ResourceType}/{ResourceId} as part of {OperationType} operation ({OperationName})", resource.TypeName.SanitizeForLog(), resource.Id.SanitizeForLog(), operation.OperationType, operation.Name.SanitizeForLog());
                                }
                                else if (operationResult.SuccessCode == OperationStatus.NoAction)
                                {
                                    _logger.LogInformation("No changes made to {ResourceType}/{ResourceId} as part of {OperationType} operation ({OperationName})", resource.TypeName.SanitizeForLog(), resource.Id.SanitizeForLog(), operation.OperationType, operation.Name.SanitizeForLog());
                                }

                                _metrics.IncrementResourceChangedCounter(new List<KeyValuePair<string, object?>>() {
                                                    new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, result.Message.Key.FacilityId),
                                                    new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, correlationId),
                                                    new KeyValuePair<string, object?>(DiagnosticNames.PatientId, result.Message.Key.PatientId),
                                                    new KeyValuePair<string, object?>(DiagnosticNames.ResourceType, resource.TypeName),
                                                    new KeyValuePair<string, object?>(DiagnosticNames.OperationType, operation.OperationType.ToString())},
                                                    operationResult.SuccessCode == OperationStatus.Success);
                            }
                            else
                            {
                                _logger.LogWarning("Normalization Operation Failed ({FacilityId}, {CorrelationId}, {OperationType}): {ErrorMessage}", result.Message.Key.FacilityId.SanitizeForLog(), correlationId.SanitizeForLog(), operation.OperationType, operationResult?.ErrorMessage?.SanitizeForLog() ?? "No Operation Result Error result");
                            }
                        }
                    }
                }
                
                resourceCache.UpdateCorrelationCache(correlationId, resources, resourceType);
            }

            await ProduceResourcesNormalizedMessage(result, result.Message.Key.FacilityId, correlationId, cancellationToken);

            resourceCache.Delete(result.Message.Value.CacheKeys);
        }
    }

    private void ValidateResourcesAcquiredEvent(ConsumeResult<ResourceKey, ResourcesAcquiredValue>? message, out string correlationId) {
        if (message == null || message.Message == null)
        {
            throw new DeadLetterException("Event is null");
        }

        if (message.Message.Key == null || string.IsNullOrWhiteSpace(message.Message.Key.FacilityId) || string.IsNullOrWhiteSpace(message.Message.Key.PatientId))
        {
            throw new DeadLetterException("Malformed key in the event. Facility Id and Patient Id are required.");
        }

        if (string.IsNullOrWhiteSpace(message.Message.Value.QueryType))
        {
            throw new DeadLetterException("Malformed value in the event. QueryType is required.");
        }

        if (message.Message.Value.ScheduledReports.Count() == 0)
        {
            throw new DeadLetterException("Malformed value in the event. At least one ScheduledReport must be included.");
        }

        if (string.IsNullOrEmpty(message.Message.Value.ReportableEvent))
        {
            throw new DeadLetterException("Malformed value in the event. ReportableEvent is required.");
        }

        try
        {
            correlationId = ExtractCorrelationId(message.Message);
        }
        catch (Exception ex)
        {
            throw new DeadLetterException("Failed to extract CorrelationId from message.", ex);
        }
    }

    private async Task ProduceResourcesNormalizedMessage(ConsumeResult<ResourceKey, ResourcesAcquiredValue>? message, string facilityId, string correlationId, CancellationToken cancellationToken = default)
    {
        var headers = new Headers
        {
            new Header(NormalizationConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(correlationId))
        };

        var resourceNormalizedMessage = new ResourcesNormalizedValue
        {
            QueryType = message.Message.Value.QueryType,
            ScheduledReports = message.Message.Value.ScheduledReports,
            ReportableEvent = message.Message.Value.ReportableEvent,
            CacheType = message.Message.Value.CacheType,
            CacheKey = correlationId
        };
        Message<ResourceKey, ResourcesNormalizedValue> produceMessage = new Message<ResourceKey, ResourcesNormalizedValue>
        {
            Key = message.Message.Key,
            Headers = headers,
            Value = resourceNormalizedMessage
        };

        try
        {
            await _producer.ProduceAsync(KafkaTopic.ResourcesNormalized.ToString(), produceMessage, cancellationToken);
        }
        catch (ProduceException<ResourceKey, ResourcesNormalizedValue> ex)
        {
            _logger.LogError(ex, "Failed to produce ResourceNormalized message. FacilityId: {FacilityId}, CorrelationId: {CorrelationId}, ResourceAcquired Partition: {Partition}, ResourceAcquired Offset: {Offset}", facilityId.SanitizeForLog(), correlationId.SanitizeForLog(), message.Partition.Value, message.Offset.Value);
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
}
