using Confluent.Kafka;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{
    public class CreatePatientEvent : ICreatePatientEvent
    {
        private readonly ILogger<CreatePatientEvent> _logger;
        private readonly IProducer<string, object> _producer;
        private readonly IMigrationHoldRegistry? _holds;

        public CreatePatientEvent(ILogger<CreatePatientEvent> logger, IProducer<string, object> producer, IMigrationHoldRegistry? holds = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
            _holds = holds;
        }

        public async Task<string> Execute(PatientEvent model, string? userId = null)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Producing Patient Event");
            string correlationId = Guid.NewGuid().ToString();

            try
            {
                var headers = new Headers
                {
                    { "X-Correlation-Id", Encoding.ASCII.GetBytes(correlationId) }
                };

                var message = new Message<string, object>
                {
                    Key = KafkaKeys.ForPatient(model.Key, model.PatientId),
                    Value = new PatientEventMessage
                    {
                        FacilityId = model.Key,
                        PatientId = model.PatientId,
                        EventType = model.EventType
                    },
                    Headers = headers
                };

                MigrationHoldGuard.RefuseIfHeld(_holds, nameof(KafkaTopic.PatientEvent));
                await _producer.ProduceAsync(nameof(KafkaTopic.PatientEvent), message);
                _logger.LogKafkaProducerPatientEvent(correlationId);

                return correlationId;

            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.AddException(ex);
                _logger.LogKafkaProducerException(nameof(KafkaTopic.PatientEvent), ex.Message);
                throw;
            }

        }
    }
}
