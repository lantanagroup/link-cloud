using System.Linq.Expressions;
using LantanaGroup.Link.Report.Application.Factory;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LinqKit;
using Task = System.Threading.Tasks.Task;

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

        Task<PagedConfigModel<ReportEntry>> SearchAsync(
            string? facilityId,
            string? patientId,
            string? reportScheduleId,
            ReportingStatus? reportingStatus,
            SubmissionStatus? submissionStatus,
            bool submissionStatusIsNull,
            string? reportType,
            string? sortBy,
            SortOrder? sortOrder,
            int pageSize,
            int pageNumber,
            CancellationToken cancellationToken = default);

        Task<int> CountAsync(Expression<Func<ReportEntry, bool>> predicate, CancellationToken cancellationToken = default);

        Task<ReportEntrySummary> GetSummaryByReportScheduleIdAsync(string reportScheduleId, CancellationToken cancellationToken = default);
    }

    public class ReportEntryManager : IReportEntryManager
    {
        private readonly IDatabase _database;
        private readonly MongoDbContext _dbContext;
        private readonly MeasureReportSummaryFactory _measureReportSummaryFactory;

        public ReportEntryManager(IDatabase database, MongoDbContext dbContext, MeasureReportSummaryFactory measureReportSummaryFactory)
        {
            _database = database;
            _dbContext = dbContext;
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

            if (consumerValue.Reportable)
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

        public async Task<int> CountAsync(Expression<Func<ReportEntry, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => _dbContext.ReportEntries.Count(predicate), cancellationToken);
        }

        public async Task<PagedConfigModel<ReportEntry>> SearchAsync(
            string? facilityId,
            string? patientId,
            string? reportScheduleId,
            ReportingStatus? reportingStatus,
            SubmissionStatus? submissionStatus,
            bool submissionStatusIsNull,
            string? reportType,
            string? sortBy,
            SortOrder? sortOrder,
            int pageSize,
            int pageNumber,
            CancellationToken cancellationToken = default)
        {
            Expression<Func<ReportEntry, bool>> predicate = x => true;

            if (!string.IsNullOrWhiteSpace(facilityId))
            {
                predicate = predicate.And(q => q.FacilityId == facilityId);
            }

            if (!string.IsNullOrWhiteSpace(patientId))
            {
                predicate = predicate.And(q => q.PatientId == patientId);
            }

            if (!string.IsNullOrWhiteSpace(reportScheduleId))
            {
                predicate = predicate.And(q => q.ReportScheduleId == reportScheduleId);
            }

            if (reportingStatus.HasValue)
            {
                predicate = predicate.And(q => q.ReportingStatus == reportingStatus.Value);
            }

            if (submissionStatusIsNull)
            {
                predicate = predicate.And(q => q.SubmissionStatus == null);
            }
            else if (submissionStatus.HasValue)
            {
                predicate = predicate.And(q => q.SubmissionStatus == submissionStatus.Value);
            }

            if (!string.IsNullOrWhiteSpace(reportType))
            {
                predicate = predicate.And(q => q.MeasureReportList.Any(a => a.ReportType == reportType));
            }

            var (results, metadata) = await _database.ReportEntryRepository.SearchAsync(
                predicate,
                sortBy,
                sortOrder,
                pageSize,
                pageNumber,
                cancellationToken);

            return new PagedConfigModel<ReportEntry>(results, metadata);
        }

        public async Task<ReportEntrySummary> GetSummaryByReportScheduleIdAsync(string reportScheduleId, CancellationToken cancellationToken = default)
        {
            var entries = await _database.ReportEntryRepository.FindAsync(
                x => x.ReportScheduleId == reportScheduleId,
                cancellationToken);

            var summary = new ReportEntrySummary();

            foreach (var entry in entries)
            {
                var reportingStatusKey = entry.ReportingStatus.ToString();
                if (summary.ReportingStatusCounts.ContainsKey(reportingStatusKey))
                {
                    summary.ReportingStatusCounts[reportingStatusKey]++;
                }
                else
                {
                    summary.ReportingStatusCounts[reportingStatusKey] = 1;
                }

                // Submission Status
                var submissionStatusKey = entry.SubmissionStatus.HasValue
                    ? entry.SubmissionStatus.Value.ToString()
                    : "Pending";
                
                if (summary.SubmissionStatusCounts.ContainsKey(submissionStatusKey))
                    summary.SubmissionStatusCounts[submissionStatusKey]++;
                else
                    summary.SubmissionStatusCounts[submissionStatusKey] = 1;

                if (entry.SubmissionStatus != SubmissionStatus.NotEligable)
                {
                    foreach (var measureReport in entry.MeasureReportList)
                    {
                        if (summary.ReportTypeCounts.ContainsKey(measureReport.ReportType))
                        {
                            summary.ReportTypeCounts[measureReport.ReportType]++;
                        }
                        else
                        {
                            summary.ReportTypeCounts[measureReport.ReportType] = 1;
                        }
                    }
                }
            }

            return summary;
        }
    }
}
