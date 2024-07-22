﻿using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Census.Application.Commands;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Messages;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;
using Microsoft.Data.SqlClient;
using System.Text;

namespace LantanaGroup.Link.Census.Listeners;

public class CensusListener : BackgroundService
{
    private readonly IKafkaConsumerFactory<string, PatientIDsAcquired> _kafkaConsumerFactory;
    private readonly IKafkaProducerFactory<string, object> _kafkaProducerFactory;
    private readonly ILogger<CensusListener> _logger;
    private readonly IMediator _mediator;
    private readonly IDeadLetterExceptionHandler<string, PatientIDsAcquired> _nonTransientExceptionHandler;
    private readonly IDeadLetterExceptionHandler<string, string> _consumeErrorHandler;
    private readonly ITransientExceptionHandler<string, PatientIDsAcquired> _transientExceptionHandler;

    public CensusListener(ILogger<CensusListener> logger, IMediator mediator, IKafkaConsumerFactory<string, PatientIDsAcquired> kafkaConsumerFactory, IKafkaProducerFactory<string, object> kafkaProducerFactory, IDeadLetterExceptionHandler<string, PatientIDsAcquired> nonTransientExceptionHandler, ITransientExceptionHandler<string, PatientIDsAcquired> transientExceptionHandler, IDeadLetterExceptionHandler<string, string> consumeErrorHandler)
    {
        _logger = logger;
        _mediator = mediator;
        _kafkaConsumerFactory = kafkaConsumerFactory;
        _kafkaProducerFactory = kafkaProducerFactory;
        _nonTransientExceptionHandler = nonTransientExceptionHandler;
        _transientExceptionHandler = transientExceptionHandler;
        _consumeErrorHandler = consumeErrorHandler;

        _transientExceptionHandler.ServiceName = CensusConstants.ServiceName;
        _transientExceptionHandler.Topic = nameof(KafkaTopic.PatientIDsAcquired) + "-Retry";
        _nonTransientExceptionHandler.ServiceName = CensusConstants.ServiceName;
        _nonTransientExceptionHandler.Topic = nameof(KafkaTopic.PatientIDsAcquired) + "-Error";
        _consumeErrorHandler.ServiceName = CensusConstants.ServiceName;
        _consumeErrorHandler.Topic = nameof(KafkaTopic.PatientIDsAcquired) + "-Error";

    }

    public override async System.Threading.Tasks.Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);
    }


    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await System.Threading.Tasks.Task.Run(() => StartConsumerLoop(cancellationToken), cancellationToken);
    }

    private async System.Threading.Tasks.Task StartConsumerLoop(CancellationToken cancellationToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            GroupId = CensusConstants.ServiceName,
            EnableAutoCommit = false
        };
        using var kafkaConsumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig);

        var producerConfig = new ProducerConfig();
        using var kafkaProducer = _kafkaProducerFactory.CreateProducer(producerConfig, useOpenTelemetry: true);

        IEnumerable<BaseResponse>? responseMessages = null;
        kafkaConsumer.Subscribe(KafkaTopic.PatientIDsAcquired.ToString());
        ConsumeResult<string, PatientIDsAcquired>? rawmessage = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await kafkaConsumer.ConsumeWithInstrumentation(async (result, CancellationToken) =>
                    {
                        rawmessage = result;

                        try
                        {
                            (string facilityId, string correlationId) messageMetaData = (string.Empty, string.Empty);

                            if (rawmessage != null)
                            {                              
                                if(rawmessage.Message.Value == null)
                                {
                                    throw new DeadLetterException("Message value is null", new MissingFacilityIdException("No message value provided. Unable to process message."));
                                }

                                var msgValue = rawmessage.Message.Value;

                                try
                                {
                                    messageMetaData = ExtractFacilityIdAndCorrelationIdFromMessage(rawmessage.Message);
                                    if (string.IsNullOrWhiteSpace(messageMetaData.facilityId))
                                    {
                                        throw new TransientException("Facility ID is null or empty", new MissingFacilityIdException("No Facility ID provided. Unable to process message."));
                                    }
                                }
                                catch (MissingFacilityIdException ex)
                                {
                                    throw new TransientException("Error extracting facility ID and correlation ID: " + ex.Message, ex);
                                }

                                try
                                {
                                    responseMessages = msgValue switch
                                    {
                                        PatientIDsAcquired => await _mediator.Send(new ConsumePatientIdsAcquiredEventCommand
                                        {
                                            FacilityId = messageMetaData.facilityId,
                                            Message = msgValue,
                                        }, cancellationToken),
                                        _ => null
                                    };

                                    await ProduceEvents(responseMessages, kafkaProducer, cancellationToken);
                                }
                                catch(SqlException ex)
                                {
                                    throw new TransientException("DB Error processing message: " + ex.Message, ex);
                                }
                                catch (Exception ex)
                                {

                                    throw new DeadLetterException("Error processing message: " + ex.Message, ex);
                                }

                                kafkaConsumer.Commit(rawmessage);
                            }
                        }
                        catch (DeadLetterException ex)
                        {
                            _nonTransientExceptionHandler.Topic = rawmessage?.Topic + "-Error";
                            _nonTransientExceptionHandler.HandleException(rawmessage, ex, rawmessage.Key);
                            kafkaConsumer.Commit(rawmessage);
                        }
                        catch (TransientException ex)
                        {
                            _transientExceptionHandler.Topic = rawmessage?.Topic + "-Retry";
                            _transientExceptionHandler.HandleException(rawmessage, ex, rawmessage.Key);
                            kafkaConsumer.Commit(rawmessage);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to process Patient Event.");

                            var auditValue = new AuditEventMessage
                            {
                                FacilityId = rawmessage.Message.Key,
                                Action = AuditEventType.Query,
                                ServiceName = CensusConstants.ServiceName,
                                EventDate = DateTime.UtcNow,
                                Notes = $"Census processing failure \nException Message: {ex}",
                            };

                            await ProduceAuditMessage(null, rawmessage?.Key, auditValue);

                            _nonTransientExceptionHandler.HandleException(rawmessage, new DeadLetterException("Census Exception thrown: " + ex.Message), rawmessage.Message.Key);
                            kafkaConsumer.Commit(rawmessage);

                            //continue;
                        }

                    }, cancellationToken);
                }
                catch (ConsumeException e)
                {
                    if (e.Error.Code == ErrorCode.UnknownTopicOrPart)
                    {
                        throw new OperationCanceledException(e.Error.Reason, e);
                    }

                    var facilityId = e.ConsumerRecord.Message.Key != null ? Encoding.UTF8.GetString(e.ConsumerRecord.Message.Key) : "";

                    var converted_record = new ConsumeResult<string, string>()
                    {
                        Message = new Message<string, string>()
                        {
                            Key = facilityId,
                            Value = e.ConsumerRecord.Message.Value != null ? System.Text.Json.JsonSerializer.Serialize(e.ConsumerRecord.Message.Value) : "",
                            Headers = e.ConsumerRecord.Message.Headers
                        }
                    };

                    _consumeErrorHandler.HandleException(converted_record, new DeadLetterException("Consume Result exception: " + e.InnerException.Message), facilityId);

                    kafkaConsumer.Commit();
                    continue;
                }
                catch (Exception ex)
                {
                    _nonTransientExceptionHandler.Topic = rawmessage?.Topic + "-Error";
                    _nonTransientExceptionHandler.HandleException(rawmessage, ex, "");
                    continue;
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation($"Stopped census consumer for topic '{KafkaTopic.PatientIDsAcquired}' at {DateTime.UtcNow}");
            kafkaConsumer.Close();
            kafkaConsumer.Dispose();
        }
    }

    private async System.Threading.Tasks.Task ProduceEvents(IEnumerable<BaseResponse> events, IProducer<string, object> producer, CancellationToken cancellationToken = default)
    {
        foreach (var ev in events)
        {
            if (ev.TopicName == KafkaTopic.PatientEvent.ToString())
            {
                if (((PatientEventResponse)ev).PatientEvent == null) return;
                PatientEvent? patientEvent = ((PatientEventResponse)ev).PatientEvent;

                Headers? headers = null;
                if (ev.CorrelationId != null)
                    headers = new Headers
                        {
                            new Header(CensusConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(ev.CorrelationId))
                        };
                var message = new Message<string, object>
                {
                    Key = ev.FacilityId,
                    Headers = headers ?? null,
                    Value = patientEvent
                };

                await producer.ProduceAsync(KafkaTopic.PatientEvent.ToString(), message);
            }
        }
    }

    private async System.Threading.Tasks.Task ProduceAuditMessage(string correlationId, string key, AuditEventMessage auditEvent)
    {

        using var auditProducer = _kafkaProducerFactory.CreateAuditEventProducer();

        Headers? headers = null;

        try
        {
            await auditProducer.ProduceAsync(KafkaTopic.AuditableEventOccurred.ToString(), new Message<string, Shared.Application.Models.Kafka.AuditEventMessage>
            {
                Key = key,
                Headers = headers ?? null,
                Value = new Shared.Application.Models.Kafka.AuditEventMessage
                {
                    Action = auditEvent.Action,
                    CorrelationId = correlationId,
                    ServiceName = CensusConstants.ServiceName,
                    EventDate = auditEvent.EventDate,
                    FacilityId = key,
                    Notes = auditEvent.Notes,
                    PropertyChanges = auditEvent.PropertyChanges,
                    Resource = auditEvent.Resource
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("There was an issue sending an audit.", ex);
        }

    }

    private (string facilityId, string correlationId) ExtractFacilityIdAndCorrelationIdFromMessage(Message<string, PatientIDsAcquired> message)
    {
        var facilityId = message.Key;
        var cIBytes = message.Headers.FirstOrDefault(x => x.Key == CensusConstants.HeaderNames.CorrelationId)?.GetValueBytes();

        if (cIBytes == null || cIBytes.Length == 0)
            return (facilityId, string.Empty);


        var correlationId = Encoding.UTF8.GetString(cIBytes);

        return (facilityId, correlationId);
    }

    private IBaseMessage DeserializeMessage(string topic, string rawMessage) =>
        topic switch
        {
            CensusConstants.MessageNames.PatientIDsAcquired => DeserializePatientIdsAcquired(rawMessage),
            _ => throw new Exception($"{topic} not a valid topic. Unable to deserialize message.")
        };

    private PatientIDsAcquired DeserializePatientIdsAcquired(string jsonContent)
    {
        var patientIdsAcquiredMessage = new PatientIDsAcquired();
        jsonContent = jsonContent.Replace("\t", string.Empty);
        jsonContent = jsonContent.Replace("\n", string.Empty);
        //var jsonDoc = System.Text.Json.JsonSerializer.
        byte[] byteArray = Encoding.UTF8.GetBytes(jsonContent);
        //byte[] byteArray = Encoding.ASCII.GetBytes(contents);
        MemoryStream stream = new MemoryStream(byteArray);
        var doc = System.Text.Json.JsonDocument.Parse(stream);
        doc.RootElement.TryGetProperty("PatientIds", out var patientids);
        var patientidsStr = patientids.ToString();
        var fhirList = new FhirJsonParser().Parse<List>(patientidsStr);
        return new PatientIDsAcquired
        {
            PatientIds = fhirList
        };
    }
}
