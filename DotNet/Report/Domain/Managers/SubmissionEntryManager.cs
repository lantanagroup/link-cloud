using LantanaGroup.Link.Report.Entities;
using System.Linq.Expressions;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Factory;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Census;
using LantanaGroup.Link.Shared.Application.Models.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public interface ISubmissionEntryManager
    {
        Task<List<MeasureReportSubmissionEntryModel>> FindAsync(
            Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<MeasureReportSubmissionEntryModel?> SingleOrDefaultAsync(
            Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<MeasureReportSubmissionEntryModel> SingleAsync(
            Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<MeasureReportSubmissionEntryModel?> FirstOrDefaultAsync(
            Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<MeasureReportSubmissionEntryModel> AddAsync(MeasureReportSubmissionEntryModel entity,
            CancellationToken cancellationToken = default);

        Task<MeasureReportSubmissionEntryModel> UpdateAsync(MeasureReportSubmissionEntryModel entity,
            CancellationToken cancellationToken = default);

        Task<bool> AnyAsync(Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate, CancellationToken cancellationToken = default);

        Task<PagedConfigModel<ScheduledReportListSummary>> GetScheduledReportSummaries(
            Expression<Func<ReportScheduleModel, bool>> predicate, string sortBy, SortOrder sortOrder, int pageSize, int pageNumber,
            CancellationToken cancellationToken = default);

        Task<ScheduledReportListSummary> GetScheduledReportSummary(string facilityId, string reportId,
            CancellationToken cancellationToken = default);

        Task<PagedConfigModel<MeasureReportSummary>> GetMeasureReports(
            Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate, string sortBy, SortOrder sortOrder, int pageSize, int pageNumber,
            CancellationToken cancellationToken = default);

        Task<PagedConfigModel<ResourceSummary>> GetMeasureReportResourceSummary(
            string facilityId, string reportId, ResourceType? resourceType, int pageSize, int pageNumber,
            CancellationToken cancellationToken = default);

        Task<List<string>> GetMeasureReportResourceTypeList(
            string facilityId, string reportId, CancellationToken cancellationToken = default);
    }

    public class SubmissionEntryManager : ISubmissionEntryManager
    {

        private readonly IDatabase _database;
        private readonly ScheduledReportFactory _scheduledReportFactory;
        private readonly MeasureReportSummaryFactory _measureReportSummaryFactory;
        private readonly ResourceSummaryFactory _resourceSummaryFactory;

        public SubmissionEntryManager(IDatabase database, ScheduledReportFactory scheduledReportFactory, MeasureReportSummaryFactory measureReportSummaryFactory, ResourceSummaryFactory resourceSummaryFactory)
        {
            _database = database;
            _scheduledReportFactory = scheduledReportFactory;
            _measureReportSummaryFactory = measureReportSummaryFactory;
            _resourceSummaryFactory = resourceSummaryFactory;
        }

        public async Task<bool> AnyAsync(Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.AnyAsync(predicate, cancellationToken);
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
                summary.CensusCount = new CensusCount
                {
                    AdmittedPatients = reportEntries.Where(x => x.ReportScheduleId == summary.Id).DistinctBy(x => x.PatientId).Count(),
                    DischargedPatients = reportEntries.Where(x => x.ReportScheduleId == summary.Id && x.Status != PatientSubmissionStatus.PendingEvaluation).DistinctBy(x => x.PatientId).Count()
                };
            }
            
            return new PagedConfigModel<ScheduledReportListSummary>(summaries, searchResults.Item2);
        }
        
        public async Task<ScheduledReportListSummary> GetScheduledReportSummary(string facilityId, string reportId, CancellationToken cancellationToken = default)
        {
           var scheduledReport = await _database.ReportScheduledRepository.SingleOrDefaultAsync(x => x.FacilityId == facilityId && x.Id == reportId, cancellationToken);
           
            if (scheduledReport is null)
                throw new ArgumentNullException($"Scheduled report with ID {reportId} not found.");
           
            var summary = _scheduledReportFactory.FromDomain(scheduledReport);

            // Get individual measure report entries for this report
            var measureReportEntries = await _database.SubmissionEntryRepository
                .FindAsync(x => x.ReportScheduleId == reportId, cancellationToken); 

            // Get the initial population count for each report
            //TODO: Eventually may need to check validation results
            if (!string.IsNullOrWhiteSpace(summary?.Id))
                summary.InitialPopulationCount =
                    measureReportEntries.Count(
                        x => x.ReportScheduleId == summary.Id &&
                             x.Status != PatientSubmissionStatus.PendingEvaluation &&
                             x.Status != PatientSubmissionStatus.NotReportable
                    );
                
            // Get census information for each report
            if (summary != null)
            {
                summary.CensusCount = new CensusCount
                {
                    AdmittedPatients = measureReportEntries.Where(x => x.ReportScheduleId == summary.Id)
                        .DistinctBy(x => x.PatientId).Count(),
                    DischargedPatients = measureReportEntries
                        .Where(x => x.ReportScheduleId == summary.Id &&
                                    x.Status != PatientSubmissionStatus.PendingEvaluation).DistinctBy(x => x.PatientId)
                        .Count()
                };
            }

            return summary;
        }
        
        public async Task<PagedConfigModel<MeasureReportSummary>> GetMeasureReports(Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate, string sortBy, SortOrder sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default)
        {
            
            // Get individual measure report entries for this report
            var searchResults = await _database.SubmissionEntryRepository
                .SearchAsync(
                    predicate,
                    sortBy: sortBy,
                    sortOrder: sortOrder, 
                    pageSize: pageSize, pageNumber: pageNumber, 
                    cancellationToken); 
            
            
            // Build patient report summaries
            var measureReports = searchResults.Item1.Select(_measureReportSummaryFactory.FromDomain).ToList();
            
            return new PagedConfigModel<MeasureReportSummary>(measureReports, searchResults.Item2);
        }

        public async Task<PagedConfigModel<ResourceSummary>> GetMeasureReportResourceSummary(
            string facilityId, string reportId, ResourceType? resourceType, int pageSize, int pageNumber,
            CancellationToken cancellationToken = default)
        {
            
            
            var measureReport = await _database.SubmissionEntryRepository.SingleOrDefaultAsync(
                x => x.FacilityId == facilityId && x.Id == reportId, cancellationToken);

            if (measureReport is null)
                return new PagedConfigModel<ResourceSummary>();
            
            var resourceQuery = measureReport.ContainedResources.AsQueryable();

            if (resourceType.HasValue)
            {
                resourceQuery = resourceQuery.Where(x => x.ResourceType == resourceType.ToString());
            }

            var pagedResources = resourceQuery
                .OrderBy(x => x.ResourceType)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize).ToList();
            
            var resourceSummaries = pagedResources.Select(x => _resourceSummaryFactory.FromDomain(facilityId, reportId, measureReport.PatientId, x)).ToList();
            
            return new PagedConfigModel<ResourceSummary>(resourceSummaries, new PaginationMetadata()
            {
                PageSize = pageSize,
                PageNumber = pageNumber,
                TotalCount = resourceQuery.ToList().Count
            });
        }
        
        public async Task<List<string>> GetMeasureReportResourceTypeList(
            string facilityId, string reportId, CancellationToken cancellationToken = default)
        {
            var measureReport = await _database.SubmissionEntryRepository.SingleOrDefaultAsync(
                x => x.FacilityId == facilityId && x.Id == reportId, cancellationToken);

            if (measureReport is null || measureReport.ContainedResources.Count == 0)
                return [];
                
            var resourceList = measureReport.ContainedResources.Select(x => x.ResourceType).Distinct().Order().ToList();
            
            return resourceList;
        }

        public async Task<List<MeasureReportSubmissionEntryModel>> FindAsync(Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.FindAsync(predicate, cancellationToken);
        }

        public async Task<MeasureReportSubmissionEntryModel?> FirstOrDefaultAsync(Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.FirstOrDefaultAsync(predicate, cancellationToken);
        }


        public async Task<MeasureReportSubmissionEntryModel?> SingleOrDefaultAsync(Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.SingleOrDefaultAsync(predicate, cancellationToken);
        }

        public async Task<MeasureReportSubmissionEntryModel> SingleAsync(Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.SingleAsync(predicate, cancellationToken);
        }

        public async Task<MeasureReportSubmissionEntryModel> AddAsync(MeasureReportSubmissionEntryModel entity, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.AddAsync(entity, cancellationToken);
        }

        public async Task<MeasureReportSubmissionEntryModel> UpdateAsync(MeasureReportSubmissionEntryModel entity, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.UpdateAsync(entity, cancellationToken);
        }
    }
}
