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

        Task<MeasureReportSubmissionEntryModel?> SingleAsync(
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
            Expression<Func<ReportScheduleModel, bool>> predicate, int pageSize, int pageNumber,
            CancellationToken cancellationToken = default);

        Task<ScheduledReportSummary> GetScheduledReportSummary(string reportId,
            CancellationToken cancellationToken = default);
    }

    public class SubmissionEntryManager : ISubmissionEntryManager
    {

        private readonly IDatabase _database;
        private readonly ScheduledReportFactory _scheduledReportFactory;
        private readonly PatientReportSummaryFactory _patientReportSummaryFactory;

        public SubmissionEntryManager(IDatabase database, ScheduledReportFactory scheduledReportFactory, PatientReportSummaryFactory patientReportSummaryFactory)
        {
            _database = database;
            _scheduledReportFactory = scheduledReportFactory;
            _patientReportSummaryFactory = patientReportSummaryFactory;
        }

        public async Task<bool> AnyAsync(Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.AnyAsync(predicate, cancellationToken);
        }
        
        public async Task<PagedConfigModel<ScheduledReportListSummary>> GetScheduledReportSummaries(Expression<Func<ReportScheduleModel, bool>> predicate, int pageSize, int pageNumber, CancellationToken cancellationToken = default)
        {
            var searchResults = await _database.ReportScheduledRepository.SearchAsync(
                predicate, 
                sortBy: "CreateDate",
                sortOrder: SortOrder.Descending, 
                pageSize: pageSize, pageNumber: pageNumber, cancellationToken);
            
            var summaries = searchResults.Item1.Select(_scheduledReportFactory.FromDomainToListSummary).ToList();
            
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
        
        public async Task<ScheduledReportSummary> GetScheduledReportSummary(string reportId, CancellationToken cancellationToken = default)
        {
           var scheduledReport = await _database.ReportScheduledRepository.GetAsync(reportId, cancellationToken);
           
            if (scheduledReport == null)
                throw new ArgumentNullException($"Scheduled report with ID {reportId} not found.");
           
            var summary = _scheduledReportFactory.FromDomainToSummary(scheduledReport);

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
            summary.CensusCount = new CensusCount
            {
                AdmittedPatients = measureReportEntries.Where(x => x.ReportScheduleId == summary.Id).DistinctBy(x => x.PatientId).Count(),
                DischargedPatients = measureReportEntries.Where(x => x.ReportScheduleId == summary.Id && x.Status != PatientSubmissionStatus.PendingEvaluation).DistinctBy(x => x.PatientId).Count()
            };
            
            // Build patient report summaries
            foreach (var measureReport in measureReportEntries.OrderBy(x => x.PatientId))
            {
                var report = _patientReportSummaryFactory.FromDomain(measureReport);
                
                // Determine patient resource metrics
                var distinctResourceTypes = measureReport.ContainedResources.Select(x => x.ResourceType).Distinct();
                foreach (var resourceType in distinctResourceTypes)
                {
                    var count = measureReport.ContainedResources.Count(x => x.ResourceType == resourceType && x.CategoryType == ResourceCategoryType.Patient);

                    if (count > 0)
                    {
                        report.PatientResources.Add(new ResourceSummary()
                        {
                            ResourceType = Enum.Parse<ResourceType>(resourceType),
                            ResourceCategory = ResourceCategoryType.Patient.ToString(),
                            ResourceCount = count
                        });
                    }
                }
                
                summary.PatientReportSummaries.Add(report);
            }
            
            // Determine shared resource metrics. Since shared resources are not patient-specific, we can check just the first entry in the measure reports
            var distinctSharedResourceTypes = measureReportEntries[1].ContainedResources
                .Where(x => x.CategoryType == ResourceCategoryType.Shared)
                .Select(x => x.ResourceType).Distinct();
            
            foreach (var resourceType in distinctSharedResourceTypes)
            {
                var count = measureReportEntries[1].ContainedResources
                    .Count(x => x.ResourceType == resourceType && x.CategoryType == ResourceCategoryType.Shared);

                if (count > 0)
                {
                    summary.SharedResources.Add(new ResourceSummary()
                    {
                        ResourceType = Enum.Parse<ResourceType>(resourceType),
                        ResourceCategory = nameof(ResourceCategoryType.Shared),
                        ResourceCount = count
                    });
                }
            }

            return summary;
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

        public async Task<MeasureReportSubmissionEntryModel?> SingleAsync(Expression<Func<MeasureReportSubmissionEntryModel, bool>> predicate, CancellationToken cancellationToken = default)
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
