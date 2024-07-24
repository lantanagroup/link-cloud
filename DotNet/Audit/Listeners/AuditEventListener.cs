using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Audit.Infrastructure.Logging;
using LantanaGroup.Link.Audit.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text;
using LantanaGroup.Link.Audit.Application.Interfaces;

namespace LantanaGroup.Link.Audit.Listeners
{
    public class AuditEventListener : BackgroundService
    {
        private readonly ILogger<AuditEventListener> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IKafkaConsumerFactory<string, AuditEventMessage> _kafkaConsumerFactory;
        private readonly IDeadLetterExceptionHandler<string, AuditEventMessage> _deadLetterExceptionHandler;
        private readonly IDeadLetterExceptionHandler<string, string> _consumerExceptionDeadLetterHandler;
        private readonly ITransientExceptionHandler<string, AuditEventMessage> _transientExceptionHandler;

        public AuditEventListener(ILogger<AuditEventListener> logger, IServiceScopeFactory scopeFactory, IKafkaConsumerFactory<string, 
            AuditEventMessage> kafkaConsumerFactory, IDeadLetterExceptionHandler<string, AuditEventMessage> deadLetterExceptionHandler, 
            IDeadLetterExceptionHandler<string, string> consumerExceptionDeadLetterHandler, ITransientExceptionHandler<string, 
            AuditEventMessage> transientExceptionHandler)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentNullException(nameof(kafkaConsumerFactory));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentNullException(nameof(deadLetterExceptionHandler));
            _consumerExceptionDeadLetterHandler = consumerExceptionDeadLetterHandler ?? throw new ArgumentNullException(nameof(consumerExceptionDeadLetterHandler));
            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentNullException(nameof(transientExceptionHandler));

            //configure deadletter exception handlers
            _deadLetterExceptionHandler.ServiceName = AuditConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.AuditableEventOccurred) + "-Error";

            _consumerExceptionDeadLetterHandler.ServiceName = AuditConstants.ServiceName;
            _consumerExceptionDeadLetterHandler.Topic = nameof(KafkaTopic.AuditableEventOccurred) + "-Error";

            //configure transient exception handler
            _transientExceptionHandler.ServiceName = AuditConstants.ServiceName;
            _transientExceptionHandler.Topic = nameof(KafkaTopic.AuditableEventOccurred) + "-Retry";            
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            var config = new ConsumerConfig()
            {
                GroupId = AuditConstants.ServiceName,
                EnableAutoCommit = false
            };

            using (var _consumer = _kafkaConsumerFactory.CreateConsumer(config))
            {
                try 
                {                
                    _consumer.Subscribe(nameof(KafkaTopic.AuditableEventOccurred));                    
                    _logger.LogConsumerStarted(nameof(KafkaTopic.AuditableEventOccurred), DateTime.UtcNow);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {                         
                            await _consumer.ConsumeWithInstrumentation(async (result, cancellationToken) => {

                                try
                                {       
                                    //process the audit event
                                    var _auditEventProcessor = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IAuditEventProcessor>();
                                    _ = await _auditEventProcessor.ProcessAuditEvent(result, cancellationToken);

                                    //consume the result and offset
                                    _consumer.Commit(result);
                                }
                                catch (DeadLetterException ex)
                                {
                                    Activity.Current?.SetStatus(ActivityStatusCode.Error);
                                    Activity.Current?.RecordException(ex);

                                    //TODO: may need to make dead letter exception handler accept nulls as that is a possibility for throwing a dead letter exception
                                    _deadLetterExceptionHandler.HandleException(result, ex, result?.Message.Key);                                   
                                    _consumer.Commit(result);                                    
                                }
                                catch (TransientException ex)
                                {
                                    Activity.Current?.SetStatus(ActivityStatusCode.Error);
                                    Activity.Current?.RecordException(ex);                                   
                                    _transientExceptionHandler.HandleException(result, ex, result.Message.Key);
                                    _consumer.Commit(result);
                                }

                            }, cancellationToken);          

                        }                        
                        catch (ConsumeException ex)
                        {
                            Activity.Current?.SetStatus(ActivityStatusCode.Error);
                            Activity.Current?.RecordException(ex);
                            _logger.LogConsumerException(nameof(KafkaTopic.AuditableEventOccurred), ex.Message);

                            if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                            {
                                throw new OperationCanceledException(ex.Error.Reason, ex);
                            }

                            var facilityId = ex.ConsumerRecord.Message.Key != null ? Encoding.UTF8.GetString(ex.ConsumerRecord.Message.Key) : "";

                            var converted_record = new ConsumeResult<string, string>()
                            {
                                Message = new Message<string, string>()
                                {
                                    Key = facilityId,
                                    Value = ex.ConsumerRecord.Message.Value != null ? Encoding.UTF8.GetString(ex.ConsumerRecord.Message.Value) : "",
                                    Headers = ex.ConsumerRecord.Message.Headers
                                }
                            };

                            var deadLetterException = new DeadLetterException($"Consume Result exception: {ex.InnerException?.Message}");
                            _consumerExceptionDeadLetterHandler.HandleException(converted_record, deadLetterException, facilityId);

                            _consumer.Commit();
                            continue;
                        }                        
                    }

                    _consumer.Close();
                    _consumer.Dispose();

                }
                catch(OperationCanceledException oce)
                {
                    Activity.Current?.SetStatus(ActivityStatusCode.Error);
                    Activity.Current?.RecordException(oce);
                    _logger.LogOperationCanceledException(nameof(KafkaTopic.AuditableEventOccurred), oce.Message);
                    _consumer.Close();
                    _consumer.Dispose();
                }
            }
        }

    }
}
