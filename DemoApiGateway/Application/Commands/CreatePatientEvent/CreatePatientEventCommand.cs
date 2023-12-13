using Confluent.Kafka;
using LantanaGroup.Link.DemoApiGateway.Application.models.interfaces;
using LantanaGroup.Link.DemoApiGateway.Application.models.testing;
using System.Text;

namespace LantanaGroup.Link.DemoApiGateway.Application.Commands.CreatePatientEvent
{
    public class CreatePatientEventCommand : ICreatePatientEventCommand
    {
        private readonly ILogger<CreatePatientEventCommand> _logger;
        private readonly IKafkaProducerFactory _kafkaProducerFactory;

        public CreatePatientEventCommand(ILogger<CreatePatientEventCommand> logger, IKafkaProducerFactory kafkaProducerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
        }


        public async Task<string> Execute(PatientEvent model)
        {
            string correlationId = Guid.NewGuid().ToString();

            try
            {

                using (var producer = _kafkaProducerFactory.CreatePatientEventProducer("data-acquisition"))
                {
                    try
                    {
                        var headers = new Headers();
                        headers.Add("X-Correlation-Id", Encoding.ASCII.GetBytes(correlationId));

                        //write to auditable event occurred topic
                        //wait for kafka topic enum to be updated KafkaTopic.PatientDischarged -> KafkaTopic.PatientEvent                        
                        await producer.ProduceAsync("PatientEvent", new Message<string, PatientEventMessage>
                        {
                            Key = model.Key,
                            Value = new PatientEventMessage { PatientId = model.PatientId, EventType = model.EventType },
                            Headers = headers
                        });

                        _logger.LogInformation($"New Patient Event ({correlationId}) created.");
                        return correlationId;

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to generate patient event.", ex);
                        throw new ApplicationException($"Failed to generate patient event.");
                    }

                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to generate patient event.", ex);
                throw new ApplicationException($"Failed to generate patient event.");
            }

        }
    }
}
