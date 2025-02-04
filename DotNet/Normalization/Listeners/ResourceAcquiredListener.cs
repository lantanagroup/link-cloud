using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Normalization.Application.Interfaces;
using LantanaGroup.Link.Normalization.Application.Managers;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Application.Settings;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.JsonObjects;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace LantanaGroup.Link.Normalization.Listeners;

public class ResourceAcquiredListener : BackgroundService
{
    private readonly ILogger<ResourceAcquiredListener> _logger;
    private readonly IAuditService _auditService;
    private readonly IKafkaConsumerFactory<string, ResourceAcquiredMessage> _consumerFactory;
    private readonly IProducer<string, ResourceNormalizedMessage> _producer;
    private readonly IDeadLetterExceptionHandler<string, string> _consumeExceptionHandler;
    private readonly IDeadLetterExceptionHandler<string, ResourceAcquiredMessage> _deadLetterExceptionHandler;
    private readonly ITransientExceptionHandler<string, ResourceAcquiredMessage> _transientExceptionHandler;
    private bool _cancelled = false;
    private readonly INormalizationServiceMetrics _metrics;
    private readonly IServiceScopeFactory _scopeFactory;

    public ResourceAcquiredListener(
        ILogger<ResourceAcquiredListener> logger,
        IOptions<ServiceInformation> serviceInformation,
        IServiceScopeFactory scopeFactory,
        IAuditService auditService,
        IKafkaConsumerFactory<string, ResourceAcquiredMessage> consumerFactory,
        IDeadLetterExceptionHandler<string, string> consumeExceptionHandler,
        IDeadLetterExceptionHandler<string, ResourceAcquiredMessage> deadLetterExceptionHandler,
        ITransientExceptionHandler<string, ResourceAcquiredMessage> transientExceptionHandler,
        INormalizationServiceMetrics metrics,
        IProducer<string, ResourceNormalizedMessage> producer)
    {
        this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _consumerFactory = consumerFactory ?? throw new ArgumentNullException(nameof(consumerFactory));
        _consumeExceptionHandler = consumeExceptionHandler ?? throw new ArgumentNullException(nameof(consumeExceptionHandler));
        _consumeExceptionHandler.ServiceName = serviceInformation.Value.Name;
        _consumeExceptionHandler.Topic = $"{nameof(KafkaTopic.ResourceAcquired)}-Error";
        _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentNullException(nameof(deadLetterExceptionHandler));
        _deadLetterExceptionHandler.ServiceName = serviceInformation.Value.Name;
        _deadLetterExceptionHandler.Topic = $"{nameof(KafkaTopic.ResourceAcquired)}-Error";
        _transientExceptionHandler = transientExceptionHandler;
        _transientExceptionHandler.ServiceName = serviceInformation.Value.Name;
        _transientExceptionHandler.Topic = KafkaTopic.ResourceAcquiredRetry.GetStringValue();
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));

        _scopeFactory = scopeFactory;
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
    }

    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await System.Threading.Tasks.Task.Run(() => StartConsumerLoop(cancellationToken), cancellationToken);
    }

    private async System.Threading.Tasks.Task StartConsumerLoop(CancellationToken cancellationToken)
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
                        using var scope = _scopeFactory.CreateScope();
                        var normalizationService = scope.ServiceProvider.GetRequiredService<INormalizationService>();
                        var configManager = scope.ServiceProvider.GetRequiredService<INormalizationConfigManager>();

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

                        NormalizationConfig? config = null;
                        try
                        {
                            config = await configManager.SingleOrDefaultAsync(c => c.FacilityId == messageMetaData.facilityId, cancellationToken);

                            if (config == null)
                            {
                                throw new NoEntityFoundException("Config for facilityId does not exist.");
                            }
                        }
                        catch(DbContextNullException ex)
                        {
                            throw new TransientException("Database context is null.", ex);
                        }
                        catch (NoEntityFoundException ex)
                        {
                            throw new TransientException("Config for facilityId does not exist.", ex);
                        }
                        catch (Exception ex)
                        {
                            throw new DeadLetterException("An error was encountered retrieving facility configuration.", ex);
                        }

                        if (message.Message.Value.AcquisitionComplete && message.Message.Value.Resource == null)
                        {
                            _logger.LogInformation("Acquisition Complete tail message received. Producing message for measure eval.");
                            var headers = new Headers
                            {
                                new Header(NormalizationConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(messageMetaData.correlationId))
                            };
                            var resourceNormalizedMessage = new ResourceNormalizedMessage
                            {
                                AcquisitionComplete = message.Message.Value.AcquisitionComplete,
                                PatientId = message.Message.Value.PatientId ?? "",
                                QueryType = message.Message.Value.QueryType,
                                ScheduledReports = message.Message.Value.ScheduledReports,
                                ReportableEvent = message.Message.Value.ReportableEvent
                            };
                            Message<string, ResourceNormalizedMessage> produceMessage = new Message<string, ResourceNormalizedMessage>
                            {
                                Key = messageMetaData.facilityId,
                                Headers = headers,
                                Value = resourceNormalizedMessage
                            };
                            await _producer.ProduceAsync(KafkaTopic.ResourceNormalized.ToString(), produceMessage);
                            return;
                        }

                        if (config.OperationSequence == null || config.OperationSequence.Count == 0)
                        {
                            throw new DeadLetterException("No operation sequence found for facility.");
                        }

                        var opSeq = config.OperationSequence.OrderBy(x => x.Key).ToList();

                        Base resource = null;
                        try
                        {
                            resource = DeserializeResource(message.Message.Value.Resource);
                        }
                        catch (Exception ex)
                        {
                            if(ex is JsonException || ex is NotSupportedException)
                            {
                                throw new TransientException("Failed to deserialize resource.", ex);
                            }

                            throw new DeadLetterException("Failed to deserialize resource.", ex);
                        }

                        var operationCommandResult = new OperationCommandResult
                        {
                            Resource = resource,
                            PropertyChanges = new List<PropertyChangeModel>()
                        };

                        //fix resource ids
                        try
                        {
                            operationCommandResult = await normalizationService.FixResourceId(new FixResourceIDCommand
                            {
                                Resource = resource,
                                PropertyChanges = operationCommandResult.PropertyChanges
                            }, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            throw new TransientException("An error occurred fixing resource Ids.", ex);
                        }

                        try
                        {
                            foreach (var op in opSeq)
                            {
                                ConceptMapOperation conceptMapOperation = null;
                                try
                                {
                                    if (op.Value is ConceptMapOperation)
                                    {
                                        conceptMapOperation = JsonSerializer.Deserialize<ConceptMapOperation>(JsonSerializer.SerializeToElement(op.Value));
                                        JsonElement conceptMapJsonEle = (JsonElement)conceptMapOperation.FhirConceptMap;
                                        conceptMapOperation.FhirConceptMap = JsonSerializer.Deserialize<ConceptMap>(
                                            conceptMapJsonEle,
                                            new JsonSerializerOptions().ForFhir(
                                                ModelInfo.ModelInspector,
                                                new FhirJsonPocoDeserializerSettings { Validator = null }));
                                    }
                                }
                                catch (Exception ex)
                                {

                                    throw new TransientException("Error deserializing Concept Map Operation.", ex);
                                }

                                operationCommandResult = op.Value switch
                                {
                                    ConceptMapOperation => await normalizationService.ApplyConceptMap(new ApplyConceptMapCommand
                                    {
                                        Resource = resource,
                                        Operation = conceptMapOperation,
                                        PropertyChanges = operationCommandResult.PropertyChanges
                                    }, cancellationToken),
                                    ConditionalTransformationOperation => await normalizationService.ConditionalTransformation(new ConditionalTransformationCommand
                                    {
                                        Resource = resource,
                                        Operation = (ConditionalTransformationOperation)op.Value,
                                        PropertyChanges = operationCommandResult.PropertyChanges
                                    }, cancellationToken),
                                    CopyElementOperation => await normalizationService.CopyElement(new CopyElementCommand
                                    {
                                        Resource = resource,
                                        Operation = (CopyElementOperation)op.Value,
                                        PropertyChanges = operationCommandResult.PropertyChanges
                                    }, cancellationToken),
                                    CopyLocationIdentifierToTypeOperation => await normalizationService.CopyLocationIdentifierToType(new CopyLocationIdentifierToTypeCommand
                                    {
                                        Resource = resource,
                                        PropertyChanges = operationCommandResult.PropertyChanges
                                    }, cancellationToken),
                                    PeriodDateFixerOperation => await normalizationService.FixPeriodDates(new PeriodDateFixerCommand
                                    {
                                        Resource = resource,
                                        PropertyChanges = operationCommandResult.PropertyChanges
                                    }, cancellationToken),
                                    _ => await normalizationService.UnknownOperation(new UnknownOperationCommand
                                    {
                                        Resource = resource,
                                        PropertyChanges = operationCommandResult.PropertyChanges
                                    }, cancellationToken)
                                };

                                _metrics.IncrementResourceNormalizedCounter(new List<KeyValuePair<string, object?>>() {
                                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, messageMetaData.facilityId),
                                new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, messageMetaData.correlationId),
                                new KeyValuePair<string, object?>(DiagnosticNames.PatientId, message.Message.Value.PatientId),
                                new KeyValuePair<string, object?>(DiagnosticNames.Resource, operationCommandResult.Resource.TypeName),
                                new KeyValuePair<string, object?>(DiagnosticNames.QueryType, message.Message.Value.QueryType),
                                new KeyValuePair<string, object?>(DiagnosticNames.NormalizationOperation, op.Value.GetType().Name)
                            });
                            }
                        }
                        catch (TransientException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            throw new DeadLetterException("An error was encountered processing Operation Commands.", ex);
                        }

                        try
                        {
                            await _auditService.TriggerAuditEvent(new TriggerAuditEventCommand
                            {
                                CorrelationId = messageMetaData.correlationId,
                                FacilityId = messageMetaData.facilityId,
                                resourceAcquiredMessage = message.Value,
                                PropertyChanges = operationCommandResult.PropertyChanges
                            }, cancellationToken);

                            var serializedResource = JsonSerializer.SerializeToElement(operationCommandResult.Resource, new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector));

                            var headers = new Headers
                            {
                                new Header(NormalizationConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(messageMetaData.correlationId))
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
                                Key = messageMetaData.facilityId,
                                Headers = headers,
                                Value = resourceNormalizedMessage
                            };
                            await _producer.ProduceAsync(KafkaTopic.ResourceNormalized.ToString(), produceMessage);

                        }
                        catch (Exception ex)
                        {
                            throw new TransientException("An error was encountered triggering audit event.", ex);
                        }
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

    public void Cancel()
    {
        this._cancelled = true;
    }

    public async System.Threading.Tasks.Task StopAsync(CancellationToken cancellationToken)
    {
        
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

    private Base DeserializeStringToResource(string json)
    {
        return JsonSerializer.Deserialize<Resource>(json, new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector, new FhirJsonPocoDeserializerSettings { Validator = null }));
    }

    private Base DeserializeResource(object resource)
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
