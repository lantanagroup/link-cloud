using Confluent.Kafka;
using KellermanSoftware.CompareNetObjects;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Quartz;
using QueryDispatch.Application.Settings;

namespace QueryDispatch.Domain.Managers
{

    public interface IScheduledReportManager
    {
        public Task<string> createScheduledReport(ScheduledReportEntity scheduledReport);
        public Task UpdateScheduledReport(ScheduledReportEntity existingReport, ScheduledReportEntity newReport);
    }

    public class ScheduledReportManager : IScheduledReportManager
    {
        IEntityRepository<ScheduledReportEntity> _scheduledReportRepository;
        IEntityRepository<QueryDispatchConfigurationEntity> _queryDispatchRepository;

        private readonly ILogger<QueryDispatchConfigurationManager> _logger;
        private readonly IProducer<string, AuditEventMessage> _producer;
        private readonly CompareLogic _compareLogic;
        private readonly ISchedulerFactory _schedulerFactory;

        public ScheduledReportManager(ILogger<QueryDispatchConfigurationManager> logger, IDatabase database, ISchedulerFactory schedulerFactory, IProducer<string, AuditEventMessage> producer)
        {
            _queryDispatchRepository = database.QueryDispatchConfigurationRepo;
            _scheduledReportRepository = database.ScheduledReportRepo;
            _logger = logger;
            _compareLogic = new CompareLogic();
            _compareLogic.Config.MaxDifferences = 25;
            _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        }

        public async Task<string> createScheduledReport(ScheduledReportEntity scheduledReport)
        {
            try
            {
                // await _datastore.AddAsync(scheduledReport);

                await _scheduledReportRepository.AddAsync(scheduledReport);

                _logger.LogInformation($"Created schedule report for faciltiy {HtmlInputSanitizer.Sanitize(scheduledReport.FacilityId)}");

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

                    _producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                    {
                        Value = auditMessage,
                        Headers = headers
                    });

                    _producer.Flush();
                


                return scheduledReport.FacilityId;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create scheduled report for facility {HtmlInputSanitizer.Sanitize(scheduledReport.FacilityId)}.", ex);
                throw new ApplicationException($"Failed to create scheduled report for facility {HtmlInputSanitizer.Sanitize(scheduledReport.FacilityId)}.");
            }
        }

        public async Task UpdateScheduledReport(ScheduledReportEntity existingReport, ScheduledReportEntity newReport)
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

                    list.Where(d => !d.PropertyName.ToLower().Contains("createdate") && !d.PropertyName.ToLower().Contains("modifydate")).ToList().ForEach(d =>
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

                //await _dataStore.Update(existingReport);

                await _scheduledReportRepository.UpdateAsync(existingReport);

                _logger.LogInformation($"Update scheduled report type {HtmlInputSanitizer.Sanitize(newReportPeriod.ReportType)} for facility id {HtmlInputSanitizer.Sanitize(existingReport.FacilityId)}");

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

                    _producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                    {
                        Value = auditMessage,
                        Headers = headers
                    });

                    _producer.Flush();
                

            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to update scheduled report for facility id {HtmlInputSanitizer.Sanitize(existingReport.FacilityId)}.", ex);
                throw new ApplicationException($"Failed to update scheduled report for facility id {HtmlInputSanitizer.Sanitize(existingReport.FacilityId)}.");
            }
        }
    }
}
