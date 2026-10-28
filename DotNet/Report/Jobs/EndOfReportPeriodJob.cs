using Confluent.Kafka;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using ReportingStatus = LantanaGroup.Link.Report.Domain.Enums.ReportingStatus;
using SubmissionStatus = LantanaGroup.Link.Report.Domain.Enums.SubmissionStatus;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.Report.Jobs
{
    [DisallowConcurrentExecution]
    public class EndOfReportPeriodJob : IJob
    {
        private readonly ILogger<EndOfReportPeriodJob> _logger;
        private readonly IQuartzJobHelper _quartz;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly DataAcquisitionRequestedProducer _dataAcqProducer;

        public EndOfReportPeriodJob(
            ILogger<EndOfReportPeriodJob> logger,
            IQuartzJobHelper quartz,
            IServiceScopeFactory serviceScopeFactory,
            DataAcquisitionRequestedProducer dataAcqProducer)
        {
            _logger = logger;
            _quartz = quartz;
            _serviceScopeFactory = serviceScopeFactory;
            _dataAcqProducer = dataAcqProducer;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            ReportScheduleModel? schedule = null;
            try
            {
                // Get the schedule ID from the job data map
                var jobDataMap = context.JobDetail.JobDataMap;
                var scheduleId = jobDataMap.GetObject<Guid?>("ReportScheduleId");

                if (scheduleId == null)
                {
                    // Fallback: try to get from trigger data map
                    scheduleId = context.Trigger.JobDataMap?.GetObject<Guid?>("ReportScheduleId");
                }

                if (scheduleId == null)
                {
                    _logger.LogError("EndOfReportPeriodJob executed but no ReportScheduleId found in job data");
                    return;
                }

                using var scope = _serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
                var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
                var reportManifestProducer = scope.ServiceProvider.GetRequiredService<ReportManifestProducer>();

                // Fetch the schedule from the database
                schedule = await reportScheduledManager.SingleOrDefaultAsync(r => r.Id == scheduleId);

                if (schedule == null)
                {
                    _logger.LogWarning("ReportSchedule {ScheduleId} not found", scheduleId);
                    return;
                }

                _logger.LogInformation("Executing EndOfReportPeriodJob for ScheduleId {ScheduleId}", schedule.Id.SanitizeForLog());

                // Mark the end-of-period flag BEFORE attempting to produce the manifest.
                // ReportManifestProducer.Produce gates on EndOfReportPeriodJobHasRun — if
                // all patient entries already reached a terminal state (e.g., discharge
                // processing completed before the period ended), the flag must be true
                // for the manifest to be generated on this call.
                schedule.Status = ScheduleStatus.EndOfPeriod;
                schedule.EndOfReportPeriodJobHasRun = true;
                await reportScheduledManager.UpdateAsync(schedule, CancellationToken.None);

                var manifestProduced = await reportManifestProducer.Produce(schedule);

                if (!manifestProduced)
                {
                    var patientsToEvaluate = await dbContext.ReportEntry.AnyAsync(
                        x => x.ReportScheduleId == schedule.Id && x.ReportingStatus == ReportingStatus.PatientIdentified,
                        CancellationToken.None
                    );

                    if (patientsToEvaluate)
                    {
                        try
                        {
                            await _dataAcqProducer.Produce(schedule, cancellationToken: context.CancellationToken);
                        }
                        catch (ProduceException<string, DataAcquisitionRequestedValue> ex)
                        {
                            _logger.LogError(ex, "Error generating Data Acquisition Requested event for FacilityId {FacilityId}", schedule.FacilityId.SanitizeForLog());
                            throw;
                        }
                    }
                }

                await _quartz.DeleteJob(
                    identity: context.JobDetail.Key.Name,
                    group: context.JobDetail.Key.Group);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered during EndOfReportPeriodJob execution");

                if (schedule != null)
                {
                    await _quartz.RescheduleJob<EndOfReportPeriodJob>(
                        identity: context.JobDetail.Key.Name,
                        jobData: context.JobDetail.JobDataMap,
                        newStartAt: DateTimeOffset.UtcNow.AddMinutes(5),
                        group: context.JobDetail.Key.Group,
                        description: context.JobDetail.Description);
                }
            }
        }
    }
}
