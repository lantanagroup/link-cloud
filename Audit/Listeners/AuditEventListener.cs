using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Audit.Application.Commands;
using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Infrastructure;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using static LantanaGroup.Link.Audit.Settings.AuditConstants;

namespace LantanaGroup.Link.Audit.Listeners
{
    public class AuditEventListener : BackgroundService
    {
        private readonly ILogger<AuditEventListener> _logger;
        private readonly ICreateAuditEventCommand _createAuditEventCommand;      
        private readonly IAuditFactory _auditFactory;
        private readonly IKafkaConsumerFactory _kafkaConsumerFactory;      
      
        public AuditEventListener(ILogger<AuditEventListener> logger, ICreateAuditEventCommand createAuditEventCommand, IAuditFactory auditFactory, IKafkaConsumerFactory kafkaConsumerFactory, IOptions<KafkaConnection> kafkaConnection) 
        { 
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _createAuditEventCommand = createAuditEventCommand ?? throw new ArgumentNullException(nameof(createAuditEventCommand));           
            _auditFactory = auditFactory ?? throw new ArgumentNullException(nameof(auditFactory));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentNullException(nameof(kafkaConsumerFactory));     
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            using (var _consumer = _kafkaConsumerFactory.CreateAuditableEventConsumer(false))
            {
                try 
                {                
                    _consumer.Subscribe(nameof(KafkaTopic.AuditableEventOccurred));
                    _logger.LogInformation(new EventId(AuditLoggingIds.EventConsumer, "Audit Service - Auditable event consumer"), "Started auditable event consumer for topic '{KafkaTopicAuditableEventOccurred}' at {DateTimeUtcNow}.", nameof(KafkaTopic.AuditableEventOccurred), DateTime.UtcNow);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {                         
                            await _consumer.ConsumeWithInstrumentation(async (result, cancellationToken) => {

                                if (result != null && result.Message.Value != null)
                                {
                                    //using Activity? activity = ServiceActivitySource.Instance.StartActivity("Kafka - Consume Auditable Event Message");
                                    var currentActivity = Activity.Current;
                                    if(currentActivity != null)
                                    {
                                        currentActivity.AddTag("link.service", ServiceActivitySource.Instance.Name);
                                        currentActivity.AddTag("link.service.version", ServiceActivitySource.Instance.Version);
                                    }
                                    

                                    AuditEventMessage messageValue = result.Message.Value;

                                    if (result.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                                    {
                                        messageValue.CorrelationId = System.Text.Encoding.UTF8.GetString(headerValue);
                                        _logger.LogInformation(new EventId(AuditLoggingIds.EventConsumer, "Audit Service - Auditable event consumer"), "Received message with correlation ID {messageValue.CorrelationId}: {consumeResultTopic}", messageValue.CorrelationId, result.Topic);
                                    }
                                    else
                                    {
                                        _logger.LogInformation(new EventId(AuditLoggingIds.EventConsumer, "Audit Service - Auditable event consumer"), "Received message without correlation ID: {consumeResultTopic}", result.Topic);
                                    }

                                    _logger.LogInformation(new EventId(AuditLoggingIds.EventConsumer, "Audit Service - Auditable event consumer"), "Consume Event for: Facility '{consumeResultMessageKey}' had an action of '{messageValueAction}' against resource '{messageValueResource}' from the service '{messageValueServiceName}'.", result.Message.Key, messageValue.Action, messageValue.Resource, messageValue.ServiceName);

                                    //create audit event
                                    CreateAuditEventModel eventModel = _auditFactory.Create(result.Message.Key, messageValue.ServiceName, messageValue.CorrelationId, messageValue.EventDate, messageValue.UserId, messageValue.User, messageValue.Action, messageValue.Resource, messageValue.PropertyChanges, messageValue.Notes);

                                    string auditEventId = await _createAuditEventCommand.Execute(eventModel);
                                    _logger.LogInformation(new EventId(AuditLoggingIds.EventConsumer, "Audit Service - Auditable event consumer"), "Successfully created new audit event '{auditEventId}'.", auditEventId);

                                    //consume the result and offset
                                    _consumer.Commit(result);

                                }
                            }, cancellationToken);          

                        }
                        catch (ConsumeException ex)
                        {
                            _logger.LogCritical(new EventId(AuditLoggingIds.EventConsumer, "Audit Service - Auditable event consumer"), ex, "Consumer error: {ErrorReason}", ex.Error.Reason);
                            if (ex.Error.IsFatal)
                            {
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogCritical(new EventId(AuditLoggingIds.EventConsumer, "Audit Service - Auditable event consumer"), ex, "Failed to generate an audit event from kafka message: {message}", ex.Message);
                            break;
                        }
                    }

                    _consumer.Close();
                    _consumer.Dispose();

                }
                catch(OperationCanceledException oce)
                {
                    _logger.LogCritical(new EventId(AuditLoggingIds.EventConsumer, "Audit Service - Auditable event consumer"), oce, "Operation Canceled: {oceMessage}", oce.Message);
                    _consumer.Close();
                    _consumer.Dispose();
                }
            }
        }

    }
}
