using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using LantanaGroup.Link.Normalization.Application.Settings;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Normalization.Listeners;

public class ResourceAcquiredListener : BackgroundService
{
    private readonly ILogger<ResourceAcquiredListener> _logger;
    private readonly IKafkaConsumerFactory<string, ResourceAcquiredMessage> _consumerFactory;
    private readonly IProducer<string, ResourceNormalizedMessage> _producer;
    private readonly IDeadLetterExceptionHandler<string, string> _consumeExceptionHandler;
    private readonly IDeadLetterExceptionHandler<string, ResourceAcquiredMessage> _deadLetterExceptionHandler;
    private readonly ITransientExceptionHandler<string, ResourceAcquiredMessage> _transientExceptionHandler;
    private bool _cancelled = false;
    private readonly INormalizationServiceMetrics _metrics;
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly CopyPropertyOperationService _copyPropertyOperationService;
    private readonly CodeMapOperationService _codeMapOperationService;
    private readonly ConditionalTransformOperationService _conditionalTransformOperationService;
    private readonly CopyLocationOperationService _copyLocationOperationService;

    public ResourceAcquiredListener(
        ILogger<ResourceAcquiredListener> logger,
        IOptions<ServiceInformation> serviceInformation,
        IServiceScopeFactory scopeFactory,
        IKafkaConsumerFactory<string, ResourceAcquiredMessage> consumerFactory,
        IDeadLetterExceptionHandler<string, string> consumeExceptionHandler,
        IDeadLetterExceptionHandler<string, ResourceAcquiredMessage> deadLetterExceptionHandler,
        ITransientExceptionHandler<string, ResourceAcquiredMessage> transientExceptionHandler,
        INormalizationServiceMetrics metrics,
        IProducer<string, ResourceNormalizedMessage> producer,
        CopyPropertyOperationService copyPropertyOperationService,
        CodeMapOperationService codeMapOperationService,
        ConditionalTransformOperationService conditionalTransformOperationService,
        CopyLocationOperationService copyLocationOperationService)
    {
        this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _consumerFactory = consumerFactory ?? throw new ArgumentNullException(nameof(consumerFactory));
        _consumeExceptionHandler = consumeExceptionHandler ?? throw new ArgumentNullException(nameof(consumeExceptionHandler));
        _consumeExceptionHandler.ServiceName = serviceInformation.Value.ServiceName;
        _consumeExceptionHandler.Topic = $"{nameof(KafkaTopic.ResourceAcquired)}-Error";
        _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentNullException(nameof(deadLetterExceptionHandler));
        _deadLetterExceptionHandler.ServiceName = serviceInformation.Value.ServiceName;
        _deadLetterExceptionHandler.Topic = $"{nameof(KafkaTopic.ResourceAcquired)}-Error";
        _transientExceptionHandler = transientExceptionHandler;
        _transientExceptionHandler.ServiceName = serviceInformation.Value.ServiceName;
        _transientExceptionHandler.Topic = KafkaTopic.ResourceAcquiredRetry.GetStringValue();
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));

        _scopeFactory = scopeFactory;
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));

        _copyPropertyOperationService = copyPropertyOperationService;
        _codeMapOperationService = codeMapOperationService ?? throw new ArgumentNullException(nameof(codeMapOperationService));
        _conditionalTransformOperationService = conditionalTransformOperationService ?? throw new ArgumentNullException(nameof(conditionalTransformOperationService));
        _copyLocationOperationService = copyLocationOperationService ?? throw new ArgumentNullException(nameof(copyLocationOperationService));
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await System.Threading.Tasks.Task.Run(() => StartConsumerLoop(cancellationToken), cancellationToken);
    }

    private async Task StartConsumerLoop(CancellationToken cancellationToken)
    {
        using var kafkaConsumer = _consumerFactory.CreateConsumer(new ConsumerConfig
        {
            GroupId = NormalizationConstants.ServiceName,
            EnableAutoCommit = false
        });

        kafkaConsumer.Subscribe(new string[] { KafkaTopic.ResourceAcquired.ToString() });

        while (!cancellationToken.IsCancellationRequested && !_cancelled)
        {
            ConsumeResult<string, ResourceAcquiredMessage>? message = default;
            try
            {                
                await kafkaConsumer.ConsumeWithInstrumentation(async (result, CancellationToken) =>
                {
                    try
                    {
                        message = result;

                        if (message.Key == null || string.IsNullOrWhiteSpace(message.Key))
                        {
                            throw new DeadLetterException("Message Key (FacilityId) is null or empty.");
                        }

                        if (
                        message.Message.Value == null 
                            || ((message.Message.Value.Resource == null 
                                || string.IsNullOrWhiteSpace(message.Message.Value.QueryType) 
                                || message.Message.Value.ScheduledReports == null)
                               && !message.Message.Value.AcquisitionComplete)
                        )
                        {
                            throw new DeadLetterException("Bad message with one of the followign reasons: \n* Null Message \n* Null Resource \n* No QueryType \n* No Scheduled Reports. Skipping message.");
                        }

                        (string facilityId, string correlationId) messageMetaData = (string.Empty, string.Empty);
                        try
                        {
                            messageMetaData = ExtractFacilityIdAndCorrelationIdFromMessage(message.Message);
                        }
                        catch (Exception ex)
                        {
                            throw new DeadLetterException("Failed to extract FacilityId and CorrelationId from message.", ex);
                        }

                        if (string.IsNullOrWhiteSpace(message.Message.Value.ReportableEvent))
                        {
                            throw new DeadLetterException("Message.Value.ReportableEvent) is null or empty");
                        }

                        if (message.Message.Value.AcquisitionComplete && message.Message.Value.Resource == null)
                        {
                            _logger.LogInformation("Acquisition Complete tail message received. Producing message for measure eval.");

                            await ProduceResourceNormalizedMessage(message, messageMetaData.facilityId, messageMetaData.correlationId, null);
                            return;
                        }

                        DomainResource resource;
                        try
                        {
                            resource = DeserializeResource(message.Message.Value.Resource);
                        }
                        catch (Exception ex)
                        {
                            if (ex is JsonException || ex is NotSupportedException)
                            {
                                throw new TransientException("Failed to deserialize resource.", ex);
                            }

                            throw new DeadLetterException("Failed to deserialize resource.", ex);
                        }

                        var operationSequenceQueries = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IOperationSequenceQueries>();

                        var sequences = await operationSequenceQueries.Search(new OperationSequenceSearchModel()
                        {
                            FacilityId = messageMetaData.facilityId,
                            ResourceType = resource.TypeName,
                        });

                        if(sequences != null && sequences.Count > 0)
                        { 
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
                                    OperationType.CopyProperty => await _copyPropertyOperationService.EnqueueOperationAsync((CopyPropertyOperation)operation, resource),
                                    OperationType.CodeMap => await _codeMapOperationService.EnqueueOperationAsync((CodeMapOperation)operation, resource),
                                    OperationType.ConditionalTransform => await _conditionalTransformOperationService.EnqueueOperationAsync((ConditionalTransformOperation)operation, resource),
                                    OperationType.CopyLocation => await _copyLocationOperationService.EnqueueOperationAsync((CopyLocationOperation)operation, resource),
                                    _ => null
                                };

                                if (operationResult != null && operationResult.SuccessCode != OperationStatus.Failure)
                                {
                                    resource = operationResult.Resource;

                                    _metrics.IncrementResourceNormalizedCounter(new List<KeyValuePair<string, object?>>() {
                                        new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, messageMetaData.facilityId),
                                        new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, messageMetaData.correlationId),
                                        new KeyValuePair<string, object?>(DiagnosticNames.PatientId, message.Message.Value.PatientId),
                                        new KeyValuePair<string, object?>(DiagnosticNames.Resource, resource.TypeName),
                                        new KeyValuePair<string, object?>(DiagnosticNames.QueryType, message.Message.Value.QueryType),
                                        new KeyValuePair<string, object?>(DiagnosticNames.NormalizationOperation, operation.OperationType.ToString())});
                                }
                                else
                                {
                                    _logger.LogWarning($@"Normalization Operation Failed ({messageMetaData.facilityId}, {messageMetaData.correlationId}, {operation.OperationType}): {operationResult?.ErrorMessage ?? "No Operation Result Error Message"}");
                                }
                            }
                        }

                        await ProduceResourceNormalizedMessage(message, messageMetaData.facilityId, messageMetaData.correlationId, resource);
                    }
                    catch (DeadLetterException ex)
                    {
                        _deadLetterExceptionHandler.HandleException(message, ex, message.Key);
                    }
                    catch (TransientException ex)
                    {
                        _transientExceptionHandler.HandleException(message, ex, message.Key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to process Patient Event.");

                        var auditValue = new Shared.Application.Models.Kafka.AuditEventMessage
                        {
                            FacilityId = message.Message.Key,
                            Action = AuditEventType.Query,
                            ServiceName = NormalizationConstants.ServiceName,
                            EventDate = DateTime.UtcNow,
                            Notes = $"Data Acquisition processing failure \nException Message: {ex}",
                        };

                        _deadLetterExceptionHandler.HandleException(message, new DeadLetterException("Data Acquisition Exception thrown: " + ex.Message), message.Message.Key);
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

                string facilityId = Encoding.UTF8.GetString(ex.ConsumerRecord?.Message?.Key ?? []);

                _consumeExceptionHandler.HandleConsumeException(ex, facilityId);
                TopicPartitionOffset? offset = ex.ConsumerRecord?.TopicPartitionOffset;
                if (offset == null)
                {
                    kafkaConsumer.Commit();
                }
                else
                {
                    kafkaConsumer.Commit( new List<TopicPartitionOffset> {
                        offset
                    });
                }
                continue;
            }            
        }
    }


    private async Task ProduceResourceNormalizedMessage(ConsumeResult<string, ResourceAcquiredMessage>? message, string facilityId, string correlationId, DomainResource? resource = null)
    {
        var serializedResource = JsonSerializer.SerializeToElement(resource, new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector));

        var headers = new Headers
        {
            new Header(NormalizationConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(correlationId))
        };

        var resourceNormalizedMessage = new ResourceNormalizedMessage
        {
            AcquisitionComplete = message.Message.Value.AcquisitionComplete,
            PatientId = message.Message.Value.PatientId ?? "",
            Resource = serializedResource,
            QueryType = message.Message.Value.QueryType,
            ScheduledReports = message.Message.Value.ScheduledReports,
            ReportableEvent = message.Message.Value.ReportableEvent
        };
        Message<string, ResourceNormalizedMessage> produceMessage = new Message<string, ResourceNormalizedMessage>
        {
            Key = facilityId,
            Headers = headers,
            Value = resourceNormalizedMessage
        };
        await _producer.ProduceAsync(KafkaTopic.ResourceNormalized.ToString(), produceMessage);
    }

    public void Cancel()
    {
        this._cancelled = true;
    }

    private (string facilityId, string correlationId) ExtractFacilityIdAndCorrelationIdFromMessage(Message<string, ResourceAcquiredMessage> message)
    {
        var facilityId = message.Key;
        var cIBytes = message.Headers.FirstOrDefault(x => x.Key == NormalizationConstants.HeaderNames.CorrelationId)?.GetValueBytes();

        if (cIBytes == null || cIBytes.Length == 0)
            throw new MissingCorrelationIdException();


        var correlationId = Encoding.UTF8.GetString(cIBytes);

        return (facilityId, correlationId);
    }

    private DomainResource DeserializeStringToResource(string json)
    {
        return JsonSerializer.Deserialize<DomainResource>(json, new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector, new FhirJsonPocoDeserializerSettings { Validator = null }));
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

}
