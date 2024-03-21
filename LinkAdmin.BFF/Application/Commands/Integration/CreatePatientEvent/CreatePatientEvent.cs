using Confluent.Kafka;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using System.Diagnostics;
using System.Text;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration.CreatePatientEvent
{
    public class CreatePatientEvent : ICreatePatientEvent
    {
        private readonly ILogger<CreatePatientEvent> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public CreatePatientEvent(ILogger<CreatePatientEvent> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));            
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        }

        public async Task<string> Execute(PatientEvent model)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Producing Patient Event");
            using var scope = _scopeFactory.CreateScope();
            var _kafkaProducerFactory = scope.ServiceProvider.GetRequiredService<IKafkaProducerFactory<string, object>>();

            string correlationId = Guid.NewGuid().ToString();

            try
            {
                var producerConfig = new ProducerConfig();                

                using (var producer = _kafkaProducerFactory.CreateProducer(producerConfig))
                {
                    try
                    {
                        var headers = new Headers();
                        headers.Add("X-Correlation-Id", Encoding.ASCII.GetBytes(correlationId));

                        var message = new Message<string, object>
                        {
                            Key = model.Key,
                            Value = new PatientEventMessage { PatientId = model.PatientId, EventType = model.EventType },
                            Headers = headers
                        };

                        await producer.ProduceAsync(nameof(KafkaTopic.PatientEvent), message);                     
                        _logger.LogKafkaProducerPatientEvent(correlationId);

                        return correlationId;

                    }
                    catch (Exception ex)
                    {
                        Activity.Current?.SetStatus(ActivityStatusCode.Error);
                        _logger.LogKafkaProducerException(nameof(KafkaTopic.PatientEvent), ex.Message);
                        throw;
                    }

                }
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                _logger.LogKafkaProducerException(nameof(KafkaTopic.PatientEvent), ex.Message);
                throw;
            }
        }
    }
}
