﻿using Confluent.Kafka;
using LantanaGroup.Link.Notification.Application.Models;
using System.Text;
using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Infrastructure;
using System.Diagnostics;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Notification.Application.Notification.Commands
{
    public class CreateAuditEventCommand : ICreateAuditEventCommand
    {
        private readonly ILogger<CreateAuditEventCommand> _logger;
        private readonly IKafkaProducerFactory _kafkaProducerFactory;

        public CreateAuditEventCommand(ILogger<CreateAuditEventCommand> logger, IKafkaProducerFactory kafkaProducerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
        }

        public async Task<bool> Execute(string? facilityId, AuditEventMessage auditEvent)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Producing Audit Event");
            
            using (var producer = _kafkaProducerFactory.CreateAuditEventProducer(useOpenTelemetry: true))
            {
                try 
                {
                    var headers = new Headers();

                    //if existing correlation id, add it to the header of the AuditableEventOccurred
                    if (!string.IsNullOrEmpty(auditEvent.CorrelationId))
                    {
                        headers.Add("X-Correlation-Id",Encoding.ASCII.GetBytes(auditEvent.CorrelationId));
                    }                    

                    //write to auditable event occurred topic
                    await producer.ProduceAsync(KafkaTopic.AuditableEventOccurred.ToString(), new Message<string, AuditEventMessage>
                    {
                        Key = facilityId ?? string.Empty,
                        Value = auditEvent,
                        Headers = headers
                    });

                    return true;
                }
                catch (Exception ex)
                {
                    Activity.Current?.SetStatus(ActivityStatusCode.Error);
                    ex.Data.Add("producer", producer);
                    ex.Data.Add("facility-id", facilityId);
                    ex.Data.Add("audit-event", auditEvent);                    
                    throw;
                }
                
            }
        }
    }
}
