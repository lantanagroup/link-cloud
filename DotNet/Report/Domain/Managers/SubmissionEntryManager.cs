using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Factory;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using System.Linq.Expressions;

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

        Task<PagedConfigModel<MeasureReportSummary>> GetMeasureReports(
            Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate, string sortBy, SortOrder sortOrder, int pageSize, int pageNumber,
            CancellationToken cancellationToken = default);

        Task<PagedConfigModel<ResourceSummary>> GetMeasureReportResourceSummary(
            string facilityId, string reportId, ResourceType? resourceType, int pageSize, int pageNumber,
            CancellationToken cancellationToken = default);

        Task<List<string>> GetMeasureReportResourceTypeList(
            string facilityId, string reportId, CancellationToken cancellationToken = default);

        Task<MeasureReportSubmissionEntryModel> UpdateStatusToValidationRequested(string patientSubmissionId, CancellationToken cancellationToken = default);
    }

    public class SubmissionEntryManager : ISubmissionEntryManager
    {

        private readonly IDatabase _database;
        private readonly MeasureReportSummaryFactory _measureReportSummaryFactory;
        private readonly ResourceSummaryFactory _resourceSummaryFactory;

        public SubmissionEntryManager(IDatabase database, MeasureReportSummaryFactory measureReportSummaryFactory, ResourceSummaryFactory resourceSummaryFactory)
        {
            _database = database;
            _measureReportSummaryFactory = measureReportSummaryFactory;
            _resourceSummaryFactory = resourceSummaryFactory;
        }

        public async Task<bool> AnyAsync(Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.AnyAsync(predicate, cancellationToken);
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

        public async Task<MeasureReportSubmissionEntryModel> UpdateStatusToValidationRequested(string patientSubmissionId, CancellationToken cancellationToken = default)
        {
            var entry = await _database.SubmissionEntryRepository.SingleOrDefaultAsync(s => s.Id == patientSubmissionId, cancellationToken);

            if (entry == null)
            {
                throw new ArgumentException($"Patient Submission Entry with ID {patientSubmissionId} not found.");
            }

            entry.Status = PatientSubmissionStatus.ValidationRequested;
            entry.ValidationStatus = ValidationStatus.Requested;

            await _database.SubmissionEntryRepository.UpdateAsync(entry, cancellationToken);

            return entry;
        }
    }
}
