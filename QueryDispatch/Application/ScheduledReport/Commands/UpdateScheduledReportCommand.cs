using Confluent.Kafka;
using KellermanSoftware.CompareNetObjects;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using QueryDispatch.Application.Settings;

namespace LantanaGroup.Link.QueryDispatch.Application.ScheduledReport.Commands
{
    public class UpdateScheduledReportCommand : IUpdateScheduledReportCommand
    {
        private readonly ILogger<UpdateScheduledReportCommand> _logger;
        private readonly IScheduledReportRepository _dataStore;
        private readonly IKafkaProducerFactory<string, AuditEventMessage> _kafkaProducerFactory;
        private readonly CompareLogic _compareLogic;

        public UpdateScheduledReportCommand(ILogger<UpdateScheduledReportCommand> logger, IScheduledReportRepository dataStore, IKafkaProducerFactory<string, AuditEventMessage> kafkaProducerFactory)   
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
            _compareLogic = new CompareLogic();
            _compareLogic.Config.MaxDifferences = 25;
        }

        public async Task Execute(ScheduledReportEntity existingReport, ScheduledReportEntity newReport)
        {
            try
            {
                List<PropertyChangeModel> propertyChanges = new List<PropertyChangeModel>();

                ReportPeriodEntity newReportPeriod = newReport.ReportPeriods[0];

                ReportPeriodEntity existingReportPeriod = existingReport.ReportPeriods.FirstOrDefault(x => x.ReportType == newReportPeriod.ReportType);
                if (existingReportPeriod != null)
                {
                    var resultChanges = _compareLogic.Compare(existingReport.ReportPeriods, newReport.ReportPeriods);
                    List<Difference> list = resultChanges.Differences;
                   
                    list.Where(d => !d.PropertyName.ToLower().Contains("createdate") &&! d.PropertyName.ToLower().Contains("modifydate")).ToList().ForEach(d =>
                    {
                        propertyChanges.Add(new PropertyChangeModel
                        {
                            PropertyName = d.PropertyName,
                            InitialPropertyValue = d.Object1Value,
                            NewPropertyValue = d.Object2Value
                        });
                    });
                    existingReportPeriod.StartDate = newReportPeriod.StartDate;
                    existingReportPeriod.EndDate = newReportPeriod.EndDate;
                    existingReportPeriod.CorrelationId = newReportPeriod.CorrelationId;
                    existingReportPeriod.ModifyDate = DateTime.UtcNow;
                }
                else
                {
                    existingReport.ReportPeriods.Add(new ReportPeriodEntity()
                    {
                        ReportType = newReportPeriod.ReportType,
                        StartDate = newReportPeriod.StartDate,
                        EndDate = newReportPeriod.EndDate,
                        CreateDate = DateTime.UtcNow,
                        ModifyDate = DateTime.UtcNow,
                        CorrelationId = newReportPeriod.CorrelationId
                    });

                }

                await _dataStore.Update(existingReport);

                _logger.LogInformation($"Update scheduled report type {newReportPeriod.ReportType} for facility id {existingReport.FacilityId}");

                using (var producer = _kafkaProducerFactory.CreateAuditEventProducer())
                {
                    var headers = new Headers
                    {
                        { "X-Correlation-Id", System.Text.Encoding.ASCII.GetBytes(newReportPeriod.CorrelationId) }
                    };

                    var auditMessage = new AuditEventMessage
                    {
                        FacilityId = existingReport.FacilityId,
                        ServiceName = QueryDispatchConstants.ServiceName,
                        Action = AuditEventType.Update,
                        EventDate = DateTime.UtcNow,
                        PropertyChanges = propertyChanges,
                        Resource = typeof(ScheduledReportEntity).Name,
                        Notes = $"Updated schedule report {existingReport.Id} for facility {existingReport.FacilityId}"
                    };

                    producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                    {
                        Value = auditMessage,
                        Headers = headers
                    });

                    producer.Flush();
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to update scheduled report for facility id {existingReport.FacilityId}.", ex);
                throw new ApplicationException($"Failed to update scheduled report for facility id {existingReport.FacilityId}.");
            }
        }
    }
}
