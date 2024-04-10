using Confluent.Kafka;
using LantanaGroup.Link.DemoApiGateway.Application.models.interfaces;
using LantanaGroup.Link.DemoApiGateway.Application.models.testing;

namespace LantanaGroup.Link.DemoApiGateway.Application.Commands
{
    public class CreateReportScheduledCommand : ICreateReportScheduledCommand
    {
        private readonly ILogger<CreateReportScheduledCommand> _logger;
        private readonly IKafkaProducerFactory _kafkaProducerFactory;

        public CreateReportScheduledCommand(ILogger<CreateReportScheduledCommand> logger, IKafkaProducerFactory kafkaProducerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
        }

        public async Task<string> Execute(ReportScheduled model)
        {
            string correlationId = Guid.NewGuid().ToString();
            List<KeyValuePair<string, object>> parameters = new List<KeyValuePair<string, object>>();
            if (model.StartDate is not null && model.EndDate is not null)
            {
                parameters.Add(new KeyValuePair<string, Object>("StartDate", model.StartDate));
                parameters.Add(new KeyValuePair<string, Object>("EndDate", model.EndDate));
            }
            else 
            {
                throw new ArgumentNullException("Start and End date for report period cannot be null");
            }
            

            try
            {

                using (var producer = _kafkaProducerFactory.CreateReportScheduledProducer("default"))
                {
                    try
                    {
                        var headers = new Headers();
                        headers.Add("X-Correlation-Id", System.Text.Encoding.ASCII.GetBytes(correlationId));

                        //write to auditable event occurred topic
                        //wait for kafka topic enum to be updated KafkaTopic.PatientDischarged -> KafkaTopic.PatientEvent                        
                        await producer.ProduceAsync("ReportScheduled", new Message<ReportScheduledKey, ReportScheduledMessage>
                        {
                            Key = new ReportScheduledKey()
                            {
                                FacilityId = model.FacilityId,
                                ReportType = model.ReportType
                            },
                            Value = new ReportScheduledMessage { Parameters = parameters },
                            Headers = headers
                        }); ;

                        _logger.LogInformation($"New Report Scheduled ({correlationId}) created.");
                        return correlationId;

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to generate report scheduled event.", ex);
                        throw new ApplicationException($"Failed to generate report scheduled event.");
                    }

                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to generate report scheduled event.", ex);
                throw new ApplicationException($"Failed to generate report scheduled event.");
            }
        }
    }
}
