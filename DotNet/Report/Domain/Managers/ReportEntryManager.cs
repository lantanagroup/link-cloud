using AngleSharp.Dom;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Factory;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Enums;
using System.Linq.Expressions;
using System.Threading;
using LantanaGroup.Link.Report.Application.Models;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public interface IReportEntryManager
    {
        Task<ReportEntry?> GetEntry(string reportScheduleId, string patientId, CancellationToken cancellationToken = default);

        Task<ReportEntry> UpdateAsync(ReportEntry entry);

        Task<ReportEntry> AddAsync(ReportEntry entry,
            CancellationToken cancellationToken);

        Task<List<ReportEntry>> FindAsync(Expression<Func<ReportEntry, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<ReportEntry?> SingleOrDefaultAsync(
            Expression<Func<ReportEntry, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<ReportEntry> UpdateAsyncWithConsumerResult(MeasureReportGeneratedValue consumerValue);

        Task<ReportEntry> UpdateAsyncWithAggregateResult(ReportEntry entry, AggregateResult aggregateResult, CancellationToken cancellationToken = default);
        Task<ReportEntry> UpdateAsyncNotReportableEntry(ReportEntry entry, CancellationToken cancellationToken = default);
    }

    public class ReportEntryManager : IReportEntryManager
    {
        private readonly IDatabase _database;
        private readonly MeasureReportSummaryFactory _measureReportSummaryFactory;

        public ReportEntryManager(IDatabase database, MeasureReportSummaryFactory measureReportSummaryFactory)
        {
            _database = database;
            _measureReportSummaryFactory = measureReportSummaryFactory;
        }

        public async Task<ReportEntry> AddAsync(ReportEntry entry, CancellationToken cancellationToken)
        {
            await _database.ReportEntryRepository.AddAsync(entry, cancellationToken);
            await _database.SaveChangesAsync();

            return entry;
        }

        public async Task<List<ReportEntry>> FindAsync(Expression<Func<ReportEntry, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportEntryRepository.FindAsync(predicate, cancellationToken);
        }

        public async Task<ReportEntry?> GetEntry(string reportScheduleId, string patientId, CancellationToken cancellationToken = default)
        {
            return (await _database.ReportEntryRepository.FindAsync(r => r.ReportScheduleId == reportScheduleId && r.PatientId == patientId, cancellationToken)).SingleOrDefault();
        }

        public async Task<ReportEntry?> SingleOrDefaultAsync(Expression<Func<ReportEntry, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportEntryRepository.SingleOrDefaultAsync(predicate, cancellationToken);
        }

        public async Task<ReportEntry> UpdateAsync(ReportEntry entry)
        {
            _database.ReportEntryRepository.Update(entry);
            await _database.SaveChangesAsync();

            return entry;
        }

        public async Task<ReportEntry> UpdateAsyncWithConsumerResult(MeasureReportGeneratedValue consumerValue)
        {
            var reportEntry = await this.GetEntry(consumerValue.ReportTrackingId, consumerValue.PatientId);

            EvaluatedMeasureReport measureReportEntry = reportEntry.MeasureReportList.First(x => x.ReportType == consumerValue.ReportType);

            measureReportEntry.MeasureReportId = consumerValue.MeasureReportId;
            measureReportEntry.MeasureReportFileName = consumerValue.MeasureReportBlobName;
            measureReportEntry.MeasureReportUri = consumerValue.MeasureReportURI;

            if (consumerValue.IsReportable)
            {
                measureReportEntry.Status = MeasureReportStatus.ReadyForValidation;
            }
            else
            {
                measureReportEntry.Status = MeasureReportStatus.NotReportable;
            }

            _database.ReportEntryRepository.Update(reportEntry);
            await _database.SaveChangesAsync();

            return reportEntry;
        }

        public async Task<ReportEntry> UpdateAsyncWithAggregateResult(ReportEntry entry, AggregateResult aggregateResult, CancellationToken cancellationToken = default)
        {
            entry.AggregateReportUri = aggregateResult.Uri.AbsoluteUri;
            entry.AggregateReportBlobName = aggregateResult.BlobName;
            entry.ModifyDate = DateTime.UtcNow;

            foreach (var measureReportResult in aggregateResult.MeasureReportResults)
            {
                entry.MeasureReportList.First(x => x.ReportType == measureReportResult.ReportType).ResourceCount = measureReportResult.ResourceCount;
            }

            _database.ReportEntryRepository.Update(entry);
            await _database.SaveChangesAsync();

            return entry;
        }

        public async Task<ReportEntry> UpdateAsyncNotReportableEntry(ReportEntry entry, CancellationToken cancellationToken = default) 
        {
            entry.ReportingStatus = ReportingStatus.NotReportable;
            entry.SubmissionStatus = SubmissionStatus.NotEligable;
            entry.ModifyDate = DateTime.UtcNow;

            _database.ReportEntryRepository.Update(entry);
            await _database.SaveChangesAsync();

            return entry;
        }
    }
}
