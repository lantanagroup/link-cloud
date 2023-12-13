using Confluent.Kafka;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.QueryDispatch.Application.ScheduledReport.Commands
{
    public class CreateScheduledReportCommand : ICreateScheduledReportCommand
    {
        private readonly ILogger<CreateScheduledReportCommand> _logger;
        private readonly IScheduledReportRepository _datastore;
        private readonly IKafkaProducerFactory<string, AuditEventMessage> _kafkaProducerFactory;

        public CreateScheduledReportCommand(ILogger<CreateScheduledReportCommand> logger, IScheduledReportRepository datastore, IKafkaProducerFactory<string, AuditEventMessage> kafkaProducerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
        }

        public async Task<string> Execute(ScheduledReportEntity scheduledReport)
        {
            try
            {
                await _datastore.AddAsync(scheduledReport);

                _logger.LogInformation($"Created schedule report for faciltiy {scheduledReport.FacilityId}");

                using (var producer = _kafkaProducerFactory.CreateAuditEventProducer())
                {
                    var headers = new Headers
                    {
                        { "X-Correlation-Id", System.Text.Encoding.ASCII.GetBytes(scheduledReport.ReportPeriods[0].CorrelationId) }
                    };

                    var auditMessage = new AuditEventMessage
                    {
                        FacilityId = scheduledReport.FacilityId,
                        ServiceName = QueryDispatchConstants.ServiceName,
                        Action = AuditEventType.Create,
                        EventDate = DateTime.UtcNow,
                        Resource = typeof(ScheduledReportEntity).Name,
                        Notes = $"Created schedule report {scheduledReport.Id} for facility {scheduledReport.FacilityId} "
                    };

                    producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                    {
                        Value = auditMessage,
                        Headers = headers
                    });

                    producer.Flush();
                }


                return scheduledReport.FacilityId;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create scheduled report for facility {scheduledReport.FacilityId}.", ex);
                throw new ApplicationException($"Failed to create scheduled report for facility {scheduledReport.FacilityId}.");
            }
        }
    }
}
