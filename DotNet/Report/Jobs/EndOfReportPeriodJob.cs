using Confluent.Kafka;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;
using Quartz;
using Task = System.Threading.Tasks.Task;

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
            ReportSchedule? schedule = null;
            try
            {
                // Get the schedule ID from the job data map
                var jobDataMap = context.JobDetail.JobDataMap;
                string? scheduleId = jobDataMap.GetObject<string>("ReportScheduleId");

                if (string.IsNullOrEmpty(scheduleId))
                {
                    // Fallback: try to get from trigger data map
                    scheduleId = context.Trigger.JobDataMap?.GetObject<string>("ReportScheduleId");
                }

                if (string.IsNullOrEmpty(scheduleId))
                {
                    _logger.LogError("EndOfReportPeriodJob executed but no ReportScheduleId found in job data");
                    return;
                }

                using var scope = _serviceScopeFactory.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
                var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
                var reportManifestProducer = scope.ServiceProvider.GetRequiredService<ReportManifestProducer>();

                // Fetch the schedule from the database
                schedule = await database.ReportScheduledRepository.GetAsync(scheduleId);
                
                if (schedule == null)
                {
                    _logger.LogWarning("ReportSchedule {ScheduleId} not found", scheduleId);
                    return;
                }

                _logger.LogInformation("Executing EndOfReportPeriodJob for ScheduleId {ScheduleId}", schedule.Id);
                
                var manifestProduced = await reportManifestProducer.Produce(schedule);

                if (!manifestProduced)
                {
                    var patientsToEvaluate = await database.ReportEntryRepository.AnyAsync(
                        x => x.ReportScheduleId == schedule.Id && x.ReportingStatus == ReportingStatus.PatientIdentified,
                        CancellationToken.None
                    );

                    if (patientsToEvaluate)
                    {
                        try
                        {
                            await _dataAcqProducer.Produce(schedule);
                        }
                        catch (ProduceException<string, DataAcquisitionRequestedValue> ex)
                        {
                            _logger.LogError(ex, "Error generating Data Acquisition Requested event for FacilityId {FacilityId}", schedule.FacilityId);
                        }
                    }
                }

                schedule.Status = ScheduleStatus.EndOfPeriod;
                schedule.EndOfReportPeriodJobHasRun = true;
                await reportScheduledManager.UpdateAsync(schedule, CancellationToken.None);

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
                        newStartAt: schedule.ReportEndDate,
                        group: context.JobDetail.Key.Group,
                        description: context.JobDetail.Description);
                }
            }
        }
    }
}
