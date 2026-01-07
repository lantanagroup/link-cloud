using System.Diagnostics.Metrics;
using System.Linq.Expressions;
using AngleSharp.Common;
using AutoMapper;
using Hl7.Fhir.Model;
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
        private readonly IReportPopulationManager _reportPopulationManager;
        private readonly IReportEntryStatusManager _reportEntryManager;

        public ReportScheduledManager(IDatabase database, ScheduledReportFactory scheduledReportFactory, IReportPopulationManager reportPopulationManager, IReportEntryStatusManager reportEntryManager)
        {
            _database = database;
            _scheduledReportFactory = scheduledReportFactory;
            _reportPopulationManager = reportPopulationManager;
            _reportEntryManager = reportEntryManager;
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
            var reportEntries = await _database.ReportEntryRepository.FindAsync(x => uniqueReportIds.Contains(x.ReportScheduleId), cancellationToken);

            foreach (var summary in summaries)
            {
                // Get the initial population count for each report
                //TODO: Eventually may need to check validation results
                var population = await _reportPopulationManager.SingleOrDefaultAsync(x => x.ReportScheduleId == summary.Id);

                summary.InitialPopulationCount = population == null ? 0 : population.GroupPopulationList.Count;

                        //reportEntries.Count(
                        //    x => x.ReportScheduleId == summary.Id &&
                        //         x.Status != PatientSubmissionStatus.PendingEvaluation &&
                        //         x.Status != PatientSubmissionStatus.NotReportable
                        //);

                // Get census information for each report
                summary.CensusCount = reportEntries.Where(x => x.ReportScheduleId == summary.Id)
                    .DistinctBy(x => x.PatientId).Count();
            }

            return new PagedConfigModel<ScheduledReportListSummary>(summaries, searchResults.Item2);
        }

        public async Task<ScheduledReportListSummary> GetScheduledReportSummary(string facilityId, string reportId, CancellationToken cancellationToken = default)
        {
            try
            {

                var scheduledReport = await this.GetReportSchedule(facilityId, reportId);

                if (scheduledReport is null)
                    throw new InvalidOperationException($"Scheduled report with ID '{reportId}' not found.");

                var summary = _scheduledReportFactory.FromDomain(scheduledReport);
                if (string.IsNullOrWhiteSpace(summary?.Id)) return summary;

                //TODO: Eventually may need to check validation results
                // Get individual measure report entries for this report
                //var measureReportEntries = await _database.ReportEntryStatusRepository
                //    .FindAsync(x => x.ReportScheduleId == reportId, cancellationToken);
                var measureReportEntries = await _reportEntryManager.FindAsync(x => x.ReportScheduleId == reportId);


                // Get the initial population count for each report
                //TODO: I don't think this is right. Need to look into adding all measures for the scheduled report
                summary.InitialPopulationCount = (await _reportPopulationManager.SingleOrDefaultAsync(x => x.ReportScheduleId == summary.Id)).GroupPopulationList.Count;
                //measureReportEntries.Count(
                //    x => x.ReportScheduleId == summary.Id &&
                //         x.Status != PatientSubmissionStatus.PendingEvaluation &&
                //         x.Status != PatientSubmissionStatus.NotReportable
                //);

                // Get census information for each report
                summary.CensusCount = measureReportEntries.Where(x => x.ReportScheduleId == summary.Id)
                    .DistinctBy(x => x.PatientId).Count();

                // Get the metrics for the scheduled report
                //var metrics = new ScheduledReportMetrics
                //{
                //    MeasureIpCounts = measureReportEntries.Where(x => x.ReportScheduleId == summary.Id && x.Status != PatientSubmissionStatus.PendingEvaluation && x.Status != PatientSubmissionStatus.NotReportable)
                //                                            .GroupBy(x => x.ReportType)
                //                                            .ToDictionary(x => MeasureNameShortener.ShortenMeasureName(x.Key), x => x.Count()),
                //    ReportStatusCounts = measureReportEntries
                //        .GroupBy(x => x.Status)
                //        .ToDictionary(x => x.Key.ToString(), x => x.Count()),
                //    ValidationStatusCounts = measureReportEntries
                //        .GroupBy(x => x.ValidationStatus)
                //        .ToDictionary(x => x.Key.ToString(), x => x.Count())
                //};


                //TODO: Change to group by report type and use name shortner after updating test data to have reportType in population document
                var pop = (await _reportPopulationManager.FindAsync(x => x.ReportScheduleId == reportId)).GroupBy(x => MeasureNameShortener.ShortenMeasureName(x.ReportType)).ToDictionary(x => x.Key, x => x.Count());

                var valid = new Dictionary<string, int>();

                foreach (var entry in measureReportEntries) {
                    if (valid.ContainsKey(entry.ReportingStatus.ToString())) {
                        valid[entry.ReportingStatus.ToString()]++;
                        continue;
                    }

                    valid.Add(entry.ReportingStatus.ToString(), 1);
                }

                var metrics = new ScheduledReportMetrics
                {
                    MeasureIpCounts = pop,
                    ReportStatusCounts = measureReportEntries
                        .GroupBy(x => x.ReportingStatus)
                        .ToDictionary(x => x.Key.ToString(), x => x.Count()),
                    ValidationStatusCounts = valid
                };

                summary.ReportMetrics = metrics;

                return summary;
            }
            catch (Exception ex) {
                //TODO: TEST OR THROW WITHOUT AND SEE WHAT HAPPENS
                throw ex;
            }
        }

    }
}
