using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Quartz;

namespace QueryDispatch.Domain.Managers
{

    public interface IScheduledReportManager
    {
        public Task<string> createScheduledReport(ScheduledReportEntity scheduledReport, CancellationToken cancellationToken = default);
        public Task UpdateScheduledReport(ScheduledReportEntity existingReport, ScheduledReportEntity newReport, CancellationToken cancellationToken = default);
    }

    public class ScheduledReportManager : IScheduledReportManager
    {
        IBaseEntityRepository<ScheduledReportEntity> _scheduledReportRepository;
        IBaseEntityRepository<QueryDispatchConfigurationEntity> _queryDispatchRepository;

        private readonly ILogger<QueryDispatchConfigurationManager> _logger;
        private readonly ISchedulerFactory _schedulerFactory;

        public ScheduledReportManager(ILogger<QueryDispatchConfigurationManager> logger, IDatabase database, ISchedulerFactory schedulerFactory)
        {
            _queryDispatchRepository = database.QueryDispatchConfigurationRepo;
            _scheduledReportRepository = database.ScheduledReportRepo;
            _logger = logger;
            _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        }

        public async Task<string> createScheduledReport(ScheduledReportEntity scheduledReport, CancellationToken cancellationToken = default)
        {
            try
            {
                await _scheduledReportRepository.AddAsync(scheduledReport, cancellationToken);

                _logger.LogInformation("Created schedule report for facility {FacilityId}", HtmlInputSanitizer.Sanitize(scheduledReport.FacilityId));

                return scheduledReport.FacilityId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create scheduled report for facility {FacilityId}", HtmlInputSanitizer.Sanitize(scheduledReport.FacilityId));
                throw new ApplicationException($"Failed to create scheduled report for facility {HtmlInputSanitizer.Sanitize(scheduledReport.FacilityId)}.");
            }
        }

        public async Task UpdateScheduledReport(ScheduledReportEntity existingReport, ScheduledReportEntity newReport, CancellationToken cancellationToken = default)
        {
            try
            {
                ReportPeriodEntity newReportPeriod = newReport.ReportPeriods[0];

                ReportPeriodEntity existingReportPeriod = existingReport.ReportPeriods.FirstOrDefault(x => x.Frequency == newReportPeriod.Frequency);
                if (existingReportPeriod != null)
                {
                    existingReportPeriod.StartDate = newReportPeriod.StartDate;
                    existingReportPeriod.EndDate = newReportPeriod.EndDate;
                    existingReportPeriod.Frequency = newReportPeriod.Frequency;
                    existingReportPeriod.ReportTypes = newReportPeriod.ReportTypes;
                    existingReportPeriod.ReportTrackingId = newReportPeriod.ReportTrackingId;
                    existingReportPeriod.ModifyDate = DateTime.UtcNow;
                }
                else
                {
                    existingReport.ReportPeriods.Add(new ReportPeriodEntity()
                    {
                        ReportTypes = newReportPeriod.ReportTypes,
                        StartDate = newReportPeriod.StartDate,
                        EndDate = newReportPeriod.EndDate,
                        Frequency = newReportPeriod.Frequency,
                        CreateDate = DateTime.UtcNow,
                        ModifyDate = DateTime.UtcNow,
                        ReportTrackingId = newReportPeriod.ReportTrackingId
                    });

                }

                await _scheduledReportRepository.UpdateAsync(existingReport, cancellationToken);

                _logger.LogInformation("Update scheduled report type {ReportTypes} for facility id {FacilityId}", HtmlInputSanitizer.Sanitize(newReportPeriod.ReportTypes.ToString()), HtmlInputSanitizer.Sanitize(existingReport.FacilityId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update scheduled report for facility id {FacilityId}", HtmlInputSanitizer.Sanitize(existingReport.FacilityId));
                throw new ApplicationException($"Failed to update scheduled report for facility id {HtmlInputSanitizer.Sanitize(existingReport.FacilityId)}.");
            }
        }
    }
}
