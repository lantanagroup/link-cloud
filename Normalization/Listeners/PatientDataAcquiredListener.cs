using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Normalization.Application.Commands;
using LantanaGroup.Link.Normalization.Application.Commands.Config;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Settings;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Linq;
using Hl7.Fhir.Specification;
using MongoDB.Driver.Linq;

namespace LantanaGroup.Link.Normalization.Listeners;

public class PatientDataAcquiredListener : BackgroundService
{
    private readonly ILogger<PatientDataAcquiredListener> _logger;
    private readonly IMediator _mediator;
    private readonly IKafkaConsumerFactory<string, PatientDataAcquiredMessage> _consumerFactory;
    private readonly IKafkaProducerFactory<string, PatientNormalizedMessage> _producerFactory;
    private readonly IDeadLetterExceptionHandler<string, PatientDataAcquiredMessage> _deadLetterExceptionHandler;
    private bool _cancelled = false;

    public PatientDataAcquiredListener(
        ILogger<PatientDataAcquiredListener> logger,
        IMediator mediator,
        IKafkaConsumerFactory<string, PatientDataAcquiredMessage> consumerFactory,
        IKafkaProducerFactory<string, PatientNormalizedMessage> producerFactory,
        IDeadLetterExceptionHandler<string, PatientDataAcquiredMessage> deadLetterExceptionHandler)
    {
        this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _consumerFactory = consumerFactory ?? throw new ArgumentNullException(nameof(consumerFactory));
        _producerFactory = producerFactory ?? throw new ArgumentNullException(nameof(producerFactory));
        _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentNullException(nameof(deadLetterExceptionHandler));
    }

    ~PatientDataAcquiredListener()
    {
     
    }

    public async System.Threading.Tasks.Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);
    }

    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await System.Threading.Tasks.Task.Run(() => StartConsumerLoop(cancellationToken), cancellationToken);
    }

    private async System.Threading.Tasks.Task StartConsumerLoop(CancellationToken cancellationToken)
    {
        using var kafkaConsumer = _consumerFactory.CreateConsumer(new ConsumerConfig
        {
            GroupId = "NormalizationService-PatientDataAcquired",
            EnableAutoCommit = false
        });
        using var kafkaProducer = _producerFactory.CreateProducer(new ProducerConfig());
        kafkaConsumer.Subscribe(new string[] { KafkaTopic.PatientAcquired.ToString() });

        while (!cancellationToken.IsCancellationRequested && !_cancelled)
        {
            ConsumeResult<string, PatientDataAcquiredMessage> message;
            try
            {
                message = kafkaConsumer.Consume(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                continue;
            }
            catch (ConsumeException ex)
            {
                // TODO: DLEH correctly handles these nulls despite declaring the corresponding parameters as non-nullable
                // Update the signature of HandleException to reflect the fact that we anticipate (and explicitly check for) potentially null values?
                // For now, use the null-forgiving operator (here and below) to suppress compiler warnings
                _deadLetterExceptionHandler.HandleException(null!, ex, AuditEventType.Create, null!);
                kafkaConsumer.Commit();
                continue;
            }

            if (message == null)
                continue;

            (string facilityId, string correlationId) messageMetaData;
            try
            {
                messageMetaData = ExtractFacilityIdAndCorrelationIdFromMessage(message.Message);
            }
            catch (Exception ex)
            {
                _deadLetterExceptionHandler.HandleException(message, ex, AuditEventType.Create, null!);
                kafkaConsumer.Commit(message);
                continue;
            }

            NormalizationConfigEntity? config = null;
            try
            {
                config = await _mediator.Send(new GetConfigurationEntityQuery
                {
                    FacilityId = messageMetaData.facilityId
                });
            }
            catch(Exception ex)
            {
                var errorMessage = $"An error was encountered retrieving facility configuration for {messageMetaData.facilityId}";

                _logger.LogError(errorMessage, ex);
                await _mediator.Send(new TriggerAuditEventCommand
                {
                    Notes = $"{errorMessage}\n{ex.Message}\n{ex.StackTrace}",
                    CorrelationId = messageMetaData.correlationId,
                    FacilityId = messageMetaData.facilityId,
                    patientDataAcquiredMessage = message.Value,
                });
                continue;
            }

            var opSeq = config.OperationSequence.OrderBy(x => x.Key).ToList();

            Bundle bundle = null;
            try
            {
                bundle = DeserializeBundle(message.Message.Value.PatientBundle);
            }
            catch(Exception ex)
            {
                _deadLetterExceptionHandler.HandleException(message, ex, AuditEventType.Create, messageMetaData.facilityId);
                kafkaConsumer.Commit(message);
                continue;
            }

            var operationCommandResult = new OperationCommandResult
            {
                Bundle = bundle,
                PropertyChanges = new List<PropertyChangeModel>()
            };

            //fix resource ids
            operationCommandResult = await _mediator.Send(new FixResourceIDCommand
            {
                Bundle = bundle,
                PropertyChanges = operationCommandResult.PropertyChanges
            });

            try
            {
                foreach (var op in opSeq)
                {
                    ConceptMapOperation conceptMapOperation = null;
                    if (op.Value is ConceptMapOperation)
                    {
                        conceptMapOperation = System.Text.Json.JsonSerializer.Deserialize<ConceptMapOperation>(JsonSerializer.SerializeToElement(op.Value));
                        JsonElement conceptMapJsonEle = (JsonElement)conceptMapOperation.FhirConceptMap;
                        conceptMapOperation.FhirConceptMap = JsonSerializer.Deserialize<ConceptMap>(
                            conceptMapJsonEle, 
                            new JsonSerializerOptions().ForFhir(
                                ModelInfo.ModelInspector, 
                                new FhirJsonPocoDeserializerSettings{ Validator = null }));
                    }

                    operationCommandResult = op.Value switch
                    {
                        ConceptMapOperation => await _mediator.Send(new ApplyConceptMapCommand
                        {
                            Bundle = bundle,
                            Operation = conceptMapOperation,
                            PropertyChanges = operationCommandResult.PropertyChanges
                        }),
                        ConditionalTransformationOperation => await _mediator.Send(new ConditionalTransformationCommand
                        {
                            Bundle = bundle,
                            Operation = (ConditionalTransformationOperation)op.Value,
                            PropertyChanges = operationCommandResult.PropertyChanges
                        }),
                        CopyElementOperation => await _mediator.Send(new CopyElementCommand
                        {
                            Bundle = bundle,
                            Operation = (CopyElementOperation)op.Value,
                            PropertyChanges = operationCommandResult.PropertyChanges
                        }),
                        CopyLocationIdentifierToTypeOperation => await _mediator.Send(new CopyLocationIdentifierToTypeCommand
                        {
                            Bundle = bundle,
                            PropertyChanges = operationCommandResult.PropertyChanges
                        }),
                        PeriodDateFixerOperation => await _mediator.Send(new PeriodDateFixerCommand
                        {
                            Bundle = bundle,
                            PropertyChanges = operationCommandResult.PropertyChanges
                        }),
                        _ => await _mediator.Send(new UnknownOperationCommand
                        {
                            Bundle = bundle,
                            PropertyChanges = operationCommandResult.PropertyChanges
                        }),
                    };
                }
            }
            catch(Exception ex )
            {
                var errorMessage = $"An error was encountered processing Operation Commands for {messageMetaData.facilityId}";

                _logger.LogError(errorMessage, ex);
                await _mediator.Send(new TriggerAuditEventCommand
                {
                    Notes = $"{errorMessage}\n{ex.Message}\n{ex.StackTrace}",
                    CorrelationId = messageMetaData.correlationId,
                    FacilityId = messageMetaData.facilityId,
                    patientDataAcquiredMessage = message.Value,
                });
                continue;
            }

            try
            {
                await _mediator.Send(new TriggerAuditEventCommand
                {
                    CorrelationId = messageMetaData.correlationId,
                    FacilityId = messageMetaData.facilityId,
                    patientDataAcquiredMessage = message.Value,
                    PropertyChanges = operationCommandResult.PropertyChanges
                });

                var serializedBundle = JsonSerializer.SerializeToElement(operationCommandResult.Bundle, new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector));

                var headers = new Headers
                    {
                        new Header(NormalizationConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(messageMetaData.correlationId))
                    };
                var patientNormalizedMessage = new PatientNormalizedMessage
                {
                    PatientId = message.Value.PatientId,
                    PatientBundle = serializedBundle,
                    ScheduledReports = message.Message.Value.ScheduledReports
                };
                Message<string, PatientNormalizedMessage> produceMessage = new Message<string, PatientNormalizedMessage>
                {
                    Key = messageMetaData.facilityId,
                    Headers = headers,
                    Value = patientNormalizedMessage
                };
                await kafkaProducer.ProduceAsync(KafkaTopic.PatientNormalized.ToString(), produceMessage);
            }
            catch(Exception ex)
            {
                var errorMessage = $"An error was encountered building/producing kafka message for {messageMetaData.facilityId}";

                _logger.LogError(errorMessage, ex);
                await _mediator.Send(new TriggerAuditEventCommand
                {
                    Notes = $"{errorMessage}\n{ex.Message}\n{ex.StackTrace}",
                    CorrelationId = messageMetaData.correlationId,
                    FacilityId = messageMetaData.facilityId,
                    patientDataAcquiredMessage = message.Value,
                });
                continue;
            }

            kafkaConsumer.Commit(message);
        }
    }

    public void Cancel()
    {
        this._cancelled = true;
    }

    public async System.Threading.Tasks.Task StopAsync(CancellationToken cancellationToken)
    {
        
    }

    private (string facilityId, string correlationId) ExtractFacilityIdAndCorrelationIdFromMessage(Message<string, PatientDataAcquiredMessage> message)
    {
        var facilityId = message.Key;
        var cIBytes = message.Headers.FirstOrDefault(x => x.Key == NormalizationConstants.HeaderNames.CorrelationId)?.GetValueBytes();

        if (cIBytes == null || cIBytes.Length == 0)
            return (facilityId, string.Empty);


        var correlationId = Encoding.UTF8.GetString(cIBytes);

        return (facilityId, correlationId);
    }

    private Bundle DeserializeBundle(object rawBundle)
    {
        Bundle bundle = null;

        return rawBundle switch
        {
            JsonElement => DeserializeStringToBundle(JsonObject.Create(((JsonElement)rawBundle)).ToJsonString()),
            string => DeserializeStringToBundle((string)rawBundle),
            _ => throw new DeserializationUnsupportedTypeException()
        };
    }

    private Bundle DeserializeStringToBundle(string json)
    {
        return JsonSerializer.Deserialize<Bundle>(
            json, 
            new JsonSerializerOptions().ForFhir(
                ModelInfo.ModelInspector, 
                new FhirJsonPocoDeserializerSettings { Validator = null }
                )
            );
    }
}
