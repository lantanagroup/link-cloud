using Confluent.Kafka;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Entities.Enums;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Quartz;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Jobs
{
    [DisallowConcurrentExecution]
    public class EndOfReportPeriodJob : IJob
    {
        private readonly ILogger<EndOfReportPeriodJob> _logger;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ReadyForValidationProducer _readyForValidationProducer;
        private readonly DataAcquisitionRequestedProducer _dataAcqProducer;

        public EndOfReportPeriodJob(
            ILogger<EndOfReportPeriodJob> logger,
            [FromKeyedServices("MongoScheduler")] ISchedulerFactory schedulerFactory,
            IServiceScopeFactory serviceScopeFactory,
            DataAcquisitionRequestedProducer dataAcqProducer,
            ReadyForValidationProducer readyForValidationProducer)
        {
            _logger = logger;
            _schedulerFactory = schedulerFactory;
            _serviceScopeFactory = serviceScopeFactory;
            _dataAcqProducer = dataAcqProducer;
            _readyForValidationProducer = readyForValidationProducer;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            string? scheduleId = context.JobDetail.JobDataMap.GetString("ReportScheduleId")
                                 ?? context.Trigger.JobDataMap?.GetString("ReportScheduleId");

            if (string.IsNullOrEmpty(scheduleId))
            {
                _logger.LogError("EndOfReportPeriodJob executed but no ReportScheduleId found in job data");
                return;
            }
            ReportSchedule? schedule = null;
            try
            {
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
                
                // remove the job from the scheduler
                await MeasureReportScheduleService.DeleteJob(schedule, await _schedulerFactory.GetScheduler());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered during EndOfReportPeriodJob execution");

                if (schedule != null)
                {
                    await MeasureReportScheduleService.RescheduleJob(schedule, await _schedulerFactory.GetScheduler());
                }
            }
        }
    }
}
