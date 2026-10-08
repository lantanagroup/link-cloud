using Confluent.Kafka;
using LantanaGroup.Link.Notification.Application.Models;
using System.Text;
using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Infrastructure;
using ServiceActivitySource = LantanaGroup.Link.Notification.Infrastructure.ServiceActivitySource;
using LantanaGroup.Link.Notification.Settings;
using System.Diagnostics;
using LantanaGroup.Link.Shared.Application.Models;
using KafkaKeys = LantanaGroup.Link.Shared.Application.Models.Kafka.KafkaKeys;

namespace LantanaGroup.Link.Notification.Application.Notification.Commands
{
    public class CreateAuditEventCommand : ICreateAuditEventCommand
    {
        private readonly ILogger<CreateAuditEventCommand> _logger;
        //private readonly IKafkaProducerFactory _kafkaProducerFactory;
        private readonly IProducer<string, AuditEventMessage> _producer;

        public CreateAuditEventCommand(ILogger<CreateAuditEventCommand> logger, IProducer<string, AuditEventMessage> producer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            //_kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        }

        public async Task<bool> Execute(string? facilityId, AuditEventMessage auditEvent)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Producing Audit Event");
            

                try 
                {
                    var headers = new Headers();

                    //if existing correlation id, add it to the header of the AuditableEventOccurred
                    if (!string.IsNullOrEmpty(auditEvent.CorrelationId))
                    {
                        headers.Add("X-Correlation-Id",Encoding.ASCII.GetBytes(auditEvent.CorrelationId));
                    }                    

                    if (string.IsNullOrWhiteSpace(auditEvent.FacilityId))
                    {
                        auditEvent.FacilityId = facilityId;
                    }

                    await _producer.ProduceAsync(KafkaTopic.AuditableEventOccurred.ToString(), new Message<string, AuditEventMessage>
                    {
                        Key = KafkaKeys.ForAudit(auditEvent.FacilityId, null, NotificationConstants.ServiceName),
                        Value = auditEvent,
                        Headers = headers
                    });

                    return true;
                }
                catch (Exception ex)
                {
                    Activity.Current?.SetStatus(ActivityStatusCode.Error);
                    ex.Data.Add("producer", _producer);
                    ex.Data.Add("facility-id", facilityId);
                    ex.Data.Add("audit-event", auditEvent);                    
                    throw;
                }
                
            
        }
    }
}
