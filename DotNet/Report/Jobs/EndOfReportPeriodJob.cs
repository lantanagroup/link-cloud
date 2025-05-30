using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using Quartz;


namespace LantanaGroup.Link.Report.Jobs
{
    [DisallowConcurrentExecution]
    public class EndOfReportPeriodJob : IJob
    {
        private readonly ILogger<EndOfReportPeriodJob> _logger;

        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IDatabase _database;

        private readonly SubmitReportProducer _submitReportProducer;
        private readonly ReadyForValidationProducer _readyForValidationProducer;
        private readonly DataAcquisitionRequestedProducer _dataAcqProducer;

        public EndOfReportPeriodJob(
            ILogger<EndOfReportPeriodJob> logger,
            ISchedulerFactory schedulerFactory,
            IDatabase database,
            DataAcquisitionRequestedProducer dataAcqProducer,
            ReadyForValidationProducer readyForValidationProducer,
            SubmitReportProducer submitReportProducer)
        {
            _logger = logger;
            _schedulerFactory = schedulerFactory;
            _database = database;
            _dataAcqProducer = dataAcqProducer;
            _readyForValidationProducer = readyForValidationProducer;
            _submitReportProducer = submitReportProducer;
        }


        public async Task Execute(IJobExecutionContext context)
        {
            ReportScheduleModel? schedule = null;
            try
            {
                JobDataMap triggerMap = context.Trigger.JobDataMap!;

                schedule = (ReportScheduleModel)triggerMap[ReportConstants.MeasureReportSubmissionScheduler.ReportScheduleModel];

                //Make sure we get a fresh object from the DB
                schedule = await _database.ReportScheduledRepository.GetAsync(schedule.Id!);

                _logger.LogInformation($"Executing EndOfReportPeriodJob for MeasureReportScheduleModel {schedule.Id}");

                var allReady = !await _database.SubmissionEntryRepository.AnyAsync(e => e.FacilityId == schedule.FacilityId 
                                                                                            && e.ReportScheduleId == schedule.Id
                                                                                            && e.Status != PatientSubmissionStatus.NotReportable 
                                                                                            && e.Status != PatientSubmissionStatus.ValidationComplete, CancellationToken.None);
                if(allReady)
                {
                    await _submitReportProducer.Produce(schedule);
                }
                else
                {
                    var patientsToEvaluate = await _database.SubmissionEntryRepository.AnyAsync(x => x.ReportScheduleId == schedule.Id && x.Status == PatientSubmissionStatus.PendingEvaluation, CancellationToken.None);

                    if (patientsToEvaluate)
                    {
                        await _dataAcqProducer.Produce(schedule);
                    }

                    var needsValidation = (await _database.SubmissionEntryRepository.FindAsync(x => x.ReportScheduleId == schedule.Id && x.Status == PatientSubmissionStatus.ReadyForValidation && x.ValidationStatus != ValidationStatus.Requested)).ToList();

                    if(needsValidation.Any())
                    {
                        await _readyForValidationProducer.Produce(schedule, needsValidation);
                    }
                }
                
                schedule.EndOfReportPeriodJobHasRun = true;
                await _database.ReportScheduledRepository.UpdateAsync(schedule);

                // remove the job from the scheduler
                await MeasureReportScheduleService.DeleteJob(schedule, await _schedulerFactory.GetScheduler());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered during GenerateDataAcquisitionRequestsForPatientsToQuery");
                if (schedule != null)
                {
                    await MeasureReportScheduleService.RescheduleJob(schedule, await _schedulerFactory.GetScheduler());
                }
            }
        }
    }
}
