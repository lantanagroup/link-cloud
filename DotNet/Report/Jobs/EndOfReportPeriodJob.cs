using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Quartz;
using System.Threading;
using static LantanaGroup.Link.Report.KafkaProducers.ReadyForValidationProducer;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Jobs
{
    [DisallowConcurrentExecution]
    public class EndOfReportPeriodJob : IJob
    {
        private readonly ILogger<EndOfReportPeriodJob> _logger;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IDatabase _database;
        private readonly ReadyForValidationProducer _readyForValidationProducer;
        private readonly DataAcquisitionRequestedProducer _dataAcqProducer;
        private readonly ReportManifestProducer _reportManifestProducer;

        public EndOfReportPeriodJob(
            ILogger<EndOfReportPeriodJob> logger,
            [FromKeyedServices("MongoScheduler")] ISchedulerFactory schedulerFactory,
            IDatabase database,
            DataAcquisitionRequestedProducer dataAcqProducer,
            ReadyForValidationProducer readyForValidationProducer,
            ReportManifestProducer reportManifestProducer)
        {
            _logger = logger;
            _schedulerFactory = schedulerFactory;
            _database = database;
            _dataAcqProducer = dataAcqProducer;
            _readyForValidationProducer = readyForValidationProducer;
            _reportManifestProducer = reportManifestProducer;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            ReportScheduleModel? schedule = null;
            try
            {
                // Get the schedule ID from the job data map
                JobDataMap jobDataMap = context.JobDetail.JobDataMap;
                string? scheduleId = jobDataMap.GetString("ReportScheduleId");

                if (string.IsNullOrEmpty(scheduleId))
                {
                    // Fallback: try to get from trigger data map
                    scheduleId = context.Trigger.JobDataMap?.GetString("ReportScheduleId");
                }

                if (string.IsNullOrEmpty(scheduleId))
                {
                    _logger.LogError("EndOfReportPeriodJob executed but no ReportScheduleId found in job data");
                    return;
                }

                // Fetch the schedule from the database
                schedule = await _database.ReportScheduledRepository.GetAsync(scheduleId);

                if (schedule == null)
                {
                    return;
                }

                _logger.LogInformation("Executing EndOfReportPeriodJob for MeasureReportScheduleModel {ScheduleId}", schedule.Id);

                var manifestProduced = await _reportManifestProducer.Produce(schedule);

                if (!manifestProduced)
                {
                    var patientsToEvaluate = await _database.SubmissionEntryRepository.AnyAsync(x => x.ReportScheduleId == schedule.Id && x.Status == PatientSubmissionStatus.PendingEvaluation, CancellationToken.None);

                    if (patientsToEvaluate)
                    {
                        try
                        {
                            await _dataAcqProducer.Produce(schedule);
                        }
                        catch (ProduceException<string, DataAcquisitionRequestedValue> ex)
                        {
                            _logger.LogError(ex, "An error was encountered generating a Data Acquisition Requested event.\n\tFacilityId: {facilityId}\n\t", schedule.FacilityId);
                        }
                    }

                    var needsValidation = (await _database.SubmissionEntryRepository.FindAsync(x => x.ReportScheduleId == schedule.Id && x.Status == PatientSubmissionStatus.ReadyForValidation && x.ValidationStatus != ValidationStatus.Requested)).ToList();

                    if (needsValidation.Any())
                    {
                        try
                        {
                            await _readyForValidationProducer.Produce(needsValidation.Select(v => new ProduceValidationModel()
                            {
                                ReportScheduleId = schedule.Id,
                                FacilityId = v.FacilityId,
                                ReportTypes = schedule.ReportTypes,
                                PatientId = v.PatientId,
                                PayloadUri = v.PayloadUri
                            }).ToList());
                        }
                        catch (ProduceException<string, string> ex)
                        {
                            _logger.LogError(ex, "An error was encountered generating a Ready For Validation event.\n\tFacilityId: {facilityId}\n\t", schedule.FacilityId);
                        }
                    }
                }

                schedule.Status = ScheduleStatus.EndOfPeriod;
                schedule.EndOfReportPeriodJobHasRun = true;
                await _database.ReportScheduledRepository.UpdateAsync(schedule);

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
