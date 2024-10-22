using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using Quartz;
using System.Text;
using LantanaGroup.Link.Shared.Application.Utilities;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Jobs
{
    [DisallowConcurrentExecution]
    public class GenerateDataAcquisitionRequestsForPatientsToQuery : IJob
    {
        private readonly ILogger<GenerateDataAcquisitionRequestsForPatientsToQuery> _logger;
        private readonly IProducer<string, DataAcquisitionRequestedValue> _dataAcqProducer;
        private readonly IProducer<SubmitReportKey, SubmitReportValue> _submissionReportProducer;
        private readonly ISchedulerFactory _schedulerFactory;

        private readonly MeasureReportAggregator _aggregator;
        private readonly IDatabase _database;

        public GenerateDataAcquisitionRequestsForPatientsToQuery(
            ILogger<GenerateDataAcquisitionRequestsForPatientsToQuery> logger,
            ISchedulerFactory schedulerFactory,
            MeasureReportAggregator aggregator,
            IDatabase database,
            IProducer<string, DataAcquisitionRequestedValue> dataAcqProducer,
            IProducer<SubmitReportKey, SubmitReportValue> submissionReportProducer)
        {
            _logger = logger;
            _schedulerFactory = schedulerFactory;
            _aggregator = aggregator;
            _database = database;
            _dataAcqProducer = dataAcqProducer;
            _submissionReportProducer = submissionReportProducer;
        }


        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                JobDataMap triggerMap = context.Trigger.JobDataMap!;

                var schedule =
                    (ReportScheduleModel)triggerMap[
                        ReportConstants.MeasureReportSubmissionScheduler.ReportScheduleModel];

                //Make sure we get a fresh object from the DB
                schedule = await _database.ReportScheduledRepository.GetAsync(schedule.Id);

                _logger.LogInformation(
                    $"Executing GenerateDataAcquisitionRequestsForPatientsToQuery for MeasureReportScheduleModel {schedule.Id}");

                if ((schedule.PatientsToQuery?.Count ?? 0) > 0)
                {
                    ProducerConfig config = new ProducerConfig()
                    {
                        ClientId = "Report_DataAcquisitionScheduled"
                    };


                    foreach (string patientId in schedule.PatientsToQuery ?? new List<string>())
                    {
                        var darKey = schedule.FacilityId;

                        var darValue = new DataAcquisitionRequestedValue()
                        {
                            PatientId = patientId,
                            ScheduledReports = new List<ScheduledReport>()
                                {
                                    new ScheduledReport()
                                    {
                                        StartDate = schedule.ReportStartDate,
                                        EndDate = schedule.ReportEndDate,
                                        ReportType = schedule.ReportType
                                    }
                                },
                            QueryType = QueryType.Initial.ToString(),
                        };

                        var headers = new Headers
                            {
                                { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) }
                            };

                        _dataAcqProducer.Produce(nameof(KafkaTopic.DataAcquisitionRequested),
                            new Message<string, DataAcquisitionRequestedValue>
                            { Key = darKey, Value = darValue, Headers = headers });
                        _dataAcqProducer.Flush();
                    }


                    _logger.LogInformation($"DataAcquisitionRequested topics published for {schedule?.PatientsToQuery?.Count ?? 0} patients for {schedule.FacilityId} for Report Type: {schedule.ReportType} for Report Dates: {schedule.ReportStartDate:G} - {schedule.ReportEndDate:G}");
                }
                else if ((schedule.PatientsToQuery?.Count ?? 0) == 0)
                {
                    var submissionEntries =
                        await _database.SubmissionEntryRepository.FindAsync(x =>
                            x.ReportScheduleId == schedule.Id);

                    var measureReports = submissionEntries
                        .Select(e => e.MeasureReport)
                        .ToList();

                    var allReady = submissionEntries.All(x => x.ReadyForSubmission);

                    if ((schedule.PatientsToQuery?.Count ?? 0) == 0 && allReady)
                    {
                        var patientIds = submissionEntries.Select(s => s.PatientId).ToList();

                        var producerConfig = new ProducerConfig()
                        {
                            ClientId = "Report_SubmissionReportScheduled"
                        };

                        var organization = FhirHelperMethods.CreateOrganization(schedule.FacilityId, ReportConstants.BundleSettings.SubmittingOrganizationProfile, ReportConstants.BundleSettings.OrganizationTypeSystem,
                                                                                ReportConstants.BundleSettings.CdcOrgIdSystem, ReportConstants.BundleSettings.DataAbsentReasonExtensionUrl, ReportConstants.BundleSettings.DataAbsentReasonUnknownCode);
                        _submissionReportProducer.Produce(nameof(KafkaTopic.SubmitReport),
                            new Message<SubmitReportKey, SubmitReportValue>
                            {
                                Key = new SubmitReportKey()
                                {
                                    FacilityId = schedule.FacilityId,
                                    StartDate = schedule.ReportStartDate,
                                    EndDate = schedule.ReportEndDate
                                },
                                Value = new SubmitReportValue()
                                {
                                    PatientIds = patientIds,
                                    Organization = organization,
                                    MeasureIds = measureReports.Select(mr => mr.Measure).Distinct().ToList(),
                                    Aggregates = _aggregator.Aggregate(measureReports, organization.Id, schedule.ReportStartDate, schedule.ReportEndDate)
                                },
                                Headers = new Headers
                                {
                                    { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) }
                                }
                            });

                        _submissionReportProducer.Flush();
                    }
                }

                schedule.PatientsToQueryDataRequested = true;

                await _database.ReportScheduledRepository.UpdateAsync(schedule);

                // remove the job from the scheduler
                await MeasureReportScheduleService.DeleteJob(schedule, await _schedulerFactory.GetScheduler());
            }
            catch (Exception ex)
            {
                _logger.LogError(null, ex, $"Error encountered in GenerateDataAcquisitionRequestsForPatientsToQuery: {ex.Message + Environment.NewLine + ex.StackTrace}");
                throw;
            }
        }
    }
}
