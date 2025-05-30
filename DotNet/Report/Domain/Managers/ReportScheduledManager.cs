using System.Linq.Expressions;
using LantanaGroup.Link.Report.Application.Factory;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public interface IReportScheduledManager
    {
        Task<ReportScheduleModel?> GetReportSchedule(string facilityid, string reportId, CancellationToken cancellationToken = default);

        Task<ReportScheduleModel> UpdateAsync(ReportScheduleModel schedule,
            CancellationToken cancellationToken);

        Task<ReportScheduleModel> AddAsync(ReportScheduleModel schedule,
            CancellationToken cancellationToken);

        Task<List<ReportScheduleModel>> FindAsync(Expression<Func<ReportScheduleModel, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<ReportScheduleModel?> SingleOrDefaultAsync(
            Expression<Func<ReportScheduleModel, bool>> predicate,
            CancellationToken cancellationToken = default);
        
        Task<(List<ReportScheduleModel>, PaginationMetadata metadata)> SearchAsync(Expression<Func<ReportScheduleModel, bool>> predicate, string? sortBy, SortOrder? sortOrder, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

        Task<PagedConfigModel<ScheduledReportListSummary>> GetScheduledReportSummaries(
            Expression<Func<ReportScheduleModel, bool>> predicate, string sortBy, SortOrder sortOrder, int pageSize, int pageNumber,
            CancellationToken cancellationToken = default);

        Task<ScheduledReportListSummary> GetScheduledReportSummary(string facilityId, string reportId,
            CancellationToken cancellationToken = default);
    }


    public class ReportScheduledManager : IReportScheduledManager
    {
        private readonly IDatabase _database;
        private readonly ScheduledReportFactory _scheduledReportFactory;

        public ReportScheduledManager(IDatabase database, ScheduledReportFactory scheduledReportFactory)
        {
            _database = database;
            _scheduledReportFactory = scheduledReportFactory;
        }

        public async Task<ReportScheduleModel?> GetReportSchedule(string facilityid, string reportId, CancellationToken cancellationToken = default)
        {
            // find existing report scheduled for this facility, report type, and date range
            return (await _database.ReportScheduledRepository.FindAsync(r => r.FacilityId == facilityid && r.Id == reportId, cancellationToken))?.SingleOrDefault();
        }

        public async Task<ReportScheduleModel?> SingleOrDefaultAsync(Expression<Func<ReportScheduleModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportScheduledRepository.SingleOrDefaultAsync(predicate, cancellationToken);
        }

        public async Task<(List<ReportScheduleModel>, PaginationMetadata metadata)> SearchAsync(Expression<Func<ReportScheduleModel, bool>> predicate, string? sortBy, SortOrder? sortOrder, int pageNumber, int pageSize,
            CancellationToken cancellationToken = default)
        {
            var searchResults = await _database.ReportScheduledRepository.SearchAsync(predicate, sortBy, sortOrder, pageNumber, pageSize, cancellationToken);
            
            return searchResults;
        }

        public async Task<List<ReportScheduleModel>> FindAsync(Expression<Func<ReportScheduleModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportScheduledRepository.FindAsync(predicate, cancellationToken);
        }

        public async Task<ReportScheduleModel> UpdateAsync(ReportScheduleModel schedule, CancellationToken cancellationToken)
        {
            return await _database.ReportScheduledRepository.UpdateAsync(schedule, cancellationToken);
        }

        public async Task<ReportScheduleModel> AddAsync(ReportScheduleModel schedule, CancellationToken cancellationToken)
        {
            return await _database.ReportScheduledRepository.AddAsync(schedule, cancellationToken);
        }

        public async Task<PagedConfigModel<ScheduledReportListSummary>> GetScheduledReportSummaries(Expression<Func<ReportScheduleModel, bool>> predicate, string sortBy, SortOrder sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default)
        {
            var searchResults = await _database.ReportScheduledRepository.SearchAsync(
            predicate,
            sortBy: sortBy,
                sortOrder: sortOrder,
                pageSize: pageSize, pageNumber: pageNumber, cancellationToken);

            var summaries = searchResults.Item1.Select(_scheduledReportFactory.FromDomain).ToList();

            // Get Census and IP information from individual measure report entries
            var uniqueReportIds = summaries.Select(x => x.Id).Distinct().ToList();
            var reportEntries = await _database.SubmissionEntryRepository
                .FindAsync(x => uniqueReportIds.Contains(x.ReportScheduleId), cancellationToken);

            foreach (var summary in summaries)
            {
                // Get the initial population count for each report
                //TODO: Eventually may need to check validation results
                if (!string.IsNullOrWhiteSpace(summary.Id))
                    summary.InitialPopulationCount =
                        reportEntries.Count(
                            x => x.ReportScheduleId == summary.Id &&
                                 x.Status != PatientSubmissionStatus.PendingEvaluation &&
                                 x.Status != PatientSubmissionStatus.NotReportable
                        );

                // Get census information for each report
                summary.CensusCount = reportEntries.Where(x => x.ReportScheduleId == summary.Id)
                    .DistinctBy(x => x.PatientId).Count();
            }

            return new PagedConfigModel<ScheduledReportListSummary>(summaries, searchResults.Item2);
        }

        public async Task<ScheduledReportListSummary> GetScheduledReportSummary(string facilityId, string reportId, CancellationToken cancellationToken = default)
        {
            var scheduledReport = await _database.ReportScheduledRepository.SingleOrDefaultAsync(x => x.FacilityId == facilityId && x.Id == reportId, cancellationToken);

            if (scheduledReport is null)
                throw new InvalidOperationException($"Scheduled report with ID '{reportId}' not found.");

            var summary = _scheduledReportFactory.FromDomain(scheduledReport);
            if (string.IsNullOrWhiteSpace(summary?.Id)) return summary;

            //TODO: Eventually may need to check validation results
            // Get individual measure report entries for this report
            var measureReportEntries = await _database.SubmissionEntryRepository
                .FindAsync(x => x.ReportScheduleId == reportId, cancellationToken);

            // Get the initial population count for each report
            summary.InitialPopulationCount =
                measureReportEntries.Count(
                    x => x.ReportScheduleId == summary.Id &&
                         x.Status != PatientSubmissionStatus.PendingEvaluation &&
                         x.Status != PatientSubmissionStatus.NotReportable
                );

            // Get census information for each report
            summary.CensusCount = measureReportEntries.Where(x => x.ReportScheduleId == summary.Id)
                .DistinctBy(x => x.PatientId).Count();

            // Get the metrics for the scheduled report
            var metrics = new ScheduledReportMetrics
            {
                MeasureIpCounts = measureReportEntries
                    .Where(x =>
                        x.ReportScheduleId == summary.Id &&
                        x.Status != PatientSubmissionStatus.PendingEvaluation &&
                        x.Status != PatientSubmissionStatus.NotReportable)
                    .GroupBy(x => x.ReportType)
                    .ToDictionary(x => MeasureNameShortener.ShortenMeasureName(x.Key), x => x.Count()),
                ReportStatusCounts = measureReportEntries
                    .GroupBy(x => x.Status)
                    .ToDictionary(x => x.Key.ToString(), x => x.Count()),
                ValidationStatusCounts = measureReportEntries
                    .GroupBy(x => x.ValidationStatus)
                    .ToDictionary(x => x.Key.ToString(), x => x.Count())
            };

            summary.ReportMetrics = metrics;

            return summary;
        }

    }
}
