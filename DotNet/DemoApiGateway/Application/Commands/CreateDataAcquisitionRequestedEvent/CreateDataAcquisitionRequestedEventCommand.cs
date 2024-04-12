using Confluent.Kafka;
using LantanaGroup.Link.DemoApiGateway.Application.models.interfaces;
using LantanaGroup.Link.DemoApiGateway.Application.models.testing;

namespace LantanaGroup.Link.DemoApiGateway.Application.Commands.CreateDataAcquisitionRequestedEvent
{
    public class CreateDataAcquisitionRequestedEventCommand : ICreateDataAcquisitionRequestedEventCommand
    {
        private readonly ILogger<CreateDataAcquisitionRequestedEventCommand> _logger;
        private readonly IKafkaProducerFactory _kafkaProducerFactory;

        public CreateDataAcquisitionRequestedEventCommand(ILogger<CreateDataAcquisitionRequestedEventCommand> logger, IKafkaProducerFactory kafkaProducerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
        }


        public async Task<string> Execute(DataAcquisitionRequested model)
        {
            string correlationId = Guid.NewGuid().ToString();

            try
            {

                using (var producer = _kafkaProducerFactory.CreateDataAcquisitionRequestedProducer("census"))
                {
                    try
                    {
                        var headers = new Headers();
                        headers.Add("X-Correlation-Id", System.Text.Encoding.ASCII.GetBytes(correlationId));

                        //write to auditable event occurred topic
                        //wait for kafka topic enum to be updated KafkaTopic.PatientDischarged -> KafkaTopic.PatientEvent                        
                        await producer.ProduceAsync("DataAcquisitionRequested", new Message<string, DataAcquisitionRequestedMessage>
                        {
                            Key = model.Key,
                            Value = new DataAcquisitionRequestedMessage { PatientId = model.PatientId, reports = model.reports },
                            Headers = headers
                        });

                        _logger.LogInformation($"New Data Acquisition Requested ({correlationId}) created.");
                        return correlationId;

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to generate Data Acquisition Requested.", ex);
                        throw new ApplicationException($"Failed to generate Data Acquisition Requested.");
                    }

                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to generate Data Acquisition Requested.", ex);
                throw new ApplicationException($"Failed to generate Data Acquisition Requested.");
            }

        }
    }
}
