﻿using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Quartz;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Jobs
{
    [DisallowConcurrentExecution]
    public class GenerateDataAcquisitionRequestsForPatientsToQuery : IJob
    {
        private readonly ILogger<GenerateDataAcquisitionRequestsForPatientsToQuery> _logger;
        private readonly IKafkaProducerFactory<string, DataAcquisitionRequestedValue> _dataAcquisitionProducerFactory;
        private readonly IKafkaProducerFactory<SubmissionReportKey, SubmissionReportValue> _submissionProducerFactory;
        private readonly ISchedulerFactory _schedulerFactory;

        private readonly MeasureReportSubmissionBundler _bundler;
        private readonly MeasureReportAggregator _aggregator;
        private readonly IDatabase _database;

        public GenerateDataAcquisitionRequestsForPatientsToQuery(ILogger<GenerateDataAcquisitionRequestsForPatientsToQuery> logger, 
            IKafkaProducerFactory<string, DataAcquisitionRequestedValue> dataAcquisitionProducerFactory,
            IKafkaProducerFactory<SubmissionReportKey, SubmissionReportValue> submissionProducerFactory, ISchedulerFactory schedulerFactory, MeasureReportSubmissionBundler bundler, MeasureReportAggregator aggregator, IDatabase database)
        {
            _logger = logger;
            _dataAcquisitionProducerFactory = dataAcquisitionProducerFactory;
            _submissionProducerFactory = submissionProducerFactory;
            _schedulerFactory = schedulerFactory;
            _bundler = bundler;
            _aggregator = aggregator;
            _database = database;
        }


        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                JobDataMap triggerMap = context.Trigger.JobDataMap!;

                var schedule =
                    (MeasureReportScheduleModel)triggerMap[
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

                    using (var prod = _dataAcquisitionProducerFactory.CreateProducer(config))
                    {
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

                            prod.Produce(nameof(KafkaTopic.DataAcquisitionRequested),
                                new Message<string, DataAcquisitionRequestedValue>
                                    { Key = darKey, Value = darValue, Headers = headers });
                            prod.Flush();
                        }
                    }

                    _logger.LogInformation($"DataAcquisitionRequested topics published for {schedule?.PatientsToQuery?.Count ?? 0} patients for {schedule.FacilityId} for Report Type: {schedule.ReportType} for Report Dates: {schedule.ReportStartDate:G} - {schedule.ReportEndDate:G}");
                }
                else if ((schedule.PatientsToQuery?.Count ?? 0) == 0)
                {
                    var submissionEntries =
                        await _database.SubmissionEntryRepository.FindAsync(x =>
                            x.MeasureReportScheduleId == schedule.Id);

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

                        using var prod = _submissionProducerFactory.CreateProducer(producerConfig);
                        var organization = _bundler.CreateOrganization(schedule.FacilityId);
                        prod.Produce(nameof(KafkaTopic.SubmitReport),
                            new Message<SubmissionReportKey, SubmissionReportValue>
                            {
                                Key = new SubmissionReportKey()
                                {
                                    FacilityId = schedule.FacilityId,
                                    StartDate = schedule.ReportStartDate,
                                    EndDate = schedule.ReportEndDate
                                },
                                Value = new SubmissionReportValue()
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

                        prod.Flush();
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
