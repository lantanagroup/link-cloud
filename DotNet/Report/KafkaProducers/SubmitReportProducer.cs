using Confluent.Kafka;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Utilities;
using System.Text;

namespace LantanaGroup.Link.Report.KafkaProducers
{
    public class SubmitReportProducer
    {
        private readonly IDatabase _database;
        private readonly IProducer<SubmitReportKey, SubmitReportValue> _submissionReportProducer;

        private readonly MeasureReportAggregator _aggregator;

        public SubmitReportProducer(IDatabase database, MeasureReportAggregator aggregator, IProducer<SubmitReportKey, SubmitReportValue> submissionReportProducer) 
        {
            _submissionReportProducer = submissionReportProducer;
            _database = database;
            _aggregator = aggregator;
        }

        public async Task<bool> Produce(ReportScheduleModel schedule)
        {
            var submissionEntries = await _database.SubmissionEntryRepository.FindAsync(x => x.ReportScheduleId == schedule.Id);
            var measureReports = submissionEntries
                        .Select(e => e.MeasureReport)
                        .Where(report => report != null).ToList();

            var patientIds = submissionEntries.Where(s => s.Status == PatientSubmissionStatus.ValidationComplete).Select(s => s.PatientId).ToList();

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
                        ReportTrackingId = schedule.Id!,
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

            schedule.SubmitReportDateTime = DateTime.UtcNow;
            await _database.ReportScheduledRepository.UpdateAsync(schedule);

            foreach (var e in submissionEntries)
            {
                e.Status = PatientSubmissionStatus.Submitted;
                await _database.SubmissionEntryRepository.UpdateAsync(e);
            }

            return true;
        }

    }
}
