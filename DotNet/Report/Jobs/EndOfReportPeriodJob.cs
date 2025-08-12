using Confluent.Kafka;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Quartz;
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
            ISchedulerFactory schedulerFactory,
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
                JobDataMap triggerMap = context.Trigger.JobDataMap!;

                schedule = (ReportScheduleModel)triggerMap[ReportConstants.MeasureReportSubmissionScheduler.ReportScheduleModel];

                //Make sure we get a fresh object from the DB
                schedule = await _database.ReportScheduledRepository.GetAsync(schedule.Id!);

                _logger.LogInformation($"Executing EndOfReportPeriodJob for MeasureReportScheduleModel {schedule.Id}");

                var allReady = !await _database.SubmissionEntryRepository.AnyAsync(e => e.FacilityId == schedule.FacilityId
                                                                                            && e.ReportScheduleId == schedule.Id
                                                                                            && e.Status != PatientSubmissionStatus.NotReportable
                                                                                            && e.Status != PatientSubmissionStatus.ValidationComplete, CancellationToken.None);
                if (allReady)
                {
                    try
                    {
                        await _reportManifestProducer.Produce(schedule);
                    }
                    catch (ProduceException<SubmitPayloadKey, SubmitPayloadValue> ex)
                    {
                        _logger.LogError(ex, "An error was encountered generating an End of Report Period Report Manifest Submit Payload event.\n\tFacilityId: {facilityId}\n\t", schedule.FacilityId);
                    }
                }
                else
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
                _logger.LogError(ex, "Exception encountered during GenerateDataAcquisitionRequestsForPatientsToQuery");
                if (schedule != null)
                {
                    await MeasureReportScheduleService.RescheduleJob(schedule, await _schedulerFactory.GetScheduler());
                }
            }
        }
    }
}