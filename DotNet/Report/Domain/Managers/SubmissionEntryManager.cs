using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.Report.Application.Factory;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Queries;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Entities.Enums;
using LantanaGroup.Link.Shared.Application.Models.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using SortOrder = LantanaGroup.Link.Shared.Application.Enums.SortOrder;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public interface ISubmissionEntryManager
    {
        Task<List<PatientSubmissionEntry>> FindAsync(
            Expression<Func<PatientSubmissionEntry, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<PatientSubmissionEntry?> SingleOrDefaultAsync(
            Expression<Func<PatientSubmissionEntry, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<PatientSubmissionEntry> SingleAsync(
            Expression<Func<PatientSubmissionEntry, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<PatientSubmissionEntry?> FirstOrDefaultAsync(
            Expression<Func<PatientSubmissionEntry, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<PatientSubmissionEntry> AddAsync(PatientSubmissionEntry entity,
            CancellationToken cancellationToken = default);

        Task<PatientSubmissionEntry> UpdateAsync(PatientSubmissionEntryUpdateModel model,
            CancellationToken cancellationToken = default);

        Task<bool> AnyAsync(Expression<Func<PatientSubmissionEntry, bool>> predicate, CancellationToken cancellationToken = default);

        Task<PagedConfigModel<MeasureReportSummary>> GetMeasureReports(
            Expression<Func<PatientSubmissionEntry, bool>> predicate, string sortBy, SortOrder sortOrder, int pageSize, int pageNumber,
            CancellationToken cancellationToken = default);

        Task UpdateAllEntriesWithPayloadUri(string reportScheduleId, string payloadUri);

        Task UpdateStatusToValidationRequested(string reportScheduleId, string facilityId, string patientId, CancellationToken cancellationToken = default);

        Task<PatientReportSummary> GetPatients(string facilityId, string reportId, int page, int count, CancellationToken cancellationToken = default);
        Task<PatientSubmissionEntry> AddResourceAsync(PatientSubmissionEntry entry, DomainResource resource, ResourceCategoryType resourceCategoryType, CancellationToken cancellationToken = default);
    }

    public class SubmissionEntryManager : ISubmissionEntryManager
    {
        private readonly MongoDbContext _context;
        private readonly IDatabase _database;
        private readonly IResourceManager _resourceManager;
        private readonly ISubmissionEntryQueries _submissionEntryQueries;
        private readonly MeasureReportSummaryFactory _measureReportSummaryFactory;

        public SubmissionEntryManager(MongoDbContext context, IDatabase database, IResourceManager resourceManager, ISubmissionEntryQueries submissionEntryQueries, MeasureReportSummaryFactory measureReportSummaryFactory)
        {
            _context = context;
            _database = database;
            _resourceManager = resourceManager;
            _submissionEntryQueries = submissionEntryQueries;
            _measureReportSummaryFactory = measureReportSummaryFactory;
        }

        public async Task<bool> AnyAsync(Expression<Func<PatientSubmissionEntry, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.AnyAsync(predicate, cancellationToken);
        }

        public async Task<PatientReportSummary> GetPatients(string facilityId, string reportId, int page, int count, CancellationToken cancellationToken = default)
        {
            var scheduledReport = await _database.ReportScheduledRepository.SingleOrDefaultAsync(x => x.FacilityId == facilityId && x.Id == reportId, cancellationToken);

            if (scheduledReport is null) throw new ArgumentNullException($"Scheduled report with ID {reportId} not found.");

            var measureReportEntries = await _database.SubmissionEntryRepository.FindAsync(x => x.ReportScheduleId == reportId, cancellationToken);

            var patientIds = measureReportEntries.Select(x => x.PatientId).Distinct().ToList();

            var pagedPatients = patientIds.Skip((page - 1) * count).Take(count).ToList();

            var patientSummaries = new List<PatientSummary>();

            foreach (var patientId in pagedPatients)
            {
                try
                {
                    var patientResource = (await _database.ResourceRepository.FindAsync(r => r.FacilityId == facilityId && r.PatientId == patientId && r.ResourceId == patientId && r.ResourceType == "Patient", cancellationToken)).SingleOrDefault();

                    if (patientResource?.Resource is not Patient patient)
                    {
                        patientSummaries.Add(new PatientSummary { id = patientId, name = string.Empty });
                        continue;
                    }

                    var name = patient.Name?.FirstOrDefault();
                    var fullName = name != null ? $"{string.Join(" ", name.Given ?? Enumerable.Empty<string>())} {name.Family}".Trim() : string.Empty;

                    patientSummaries.Add(new PatientSummary
                    {
                        id = patientId,
                        name = fullName
                    });
                }
                catch (Exception ex)
                {
                    // Handle exception if GetResource fails
                    patientSummaries.Add(new PatientSummary
                    {
                        id = patientId,
                        name = string.Empty
                    });
                }
            }

            PatientReportSummary patientReportSummary = new PatientReportSummary();
            patientReportSummary.total = patientIds.Count;
            patientReportSummary.Patients = patientSummaries;

            return patientReportSummary;
        }

        public async Task<PagedConfigModel<MeasureReportSummary>> GetMeasureReports(Expression<Func<PatientSubmissionEntry, bool>> predicate, string sortBy, SortOrder sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default)
        {

            // Get individual measure report entries for this report
            var searchResults = await _database.SubmissionEntryRepository
                .SearchAsync(
                    predicate,
                    sortBy: sortBy,
                    sortOrder: sortOrder,
                    pageSize: pageSize, pageNumber: pageNumber,
                    cancellationToken);

            Dictionary<string, PatientReportData> patietResourceData = new Dictionary<string, PatientReportData>();
            var measureReports = new List<MeasureReportSummary>();
            foreach (var result in searchResults.Item1)
            {
                var report = _measureReportSummaryFactory.FromDomain(result);
                var key = result.FacilityId + result.ReportScheduleId + result.PatientId;

                if (!patietResourceData.ContainsKey(key))
                {
                    var patienData = await _submissionEntryQueries.GetPatientReportData(result.FacilityId, result.ReportScheduleId, result.PatientId);
                    patietResourceData.Add(key, patienData);
                }

                var data = patietResourceData[key];
                report.ResourceCount = data.ReportData[report.ReportType].Resources.Count();

                report.ResourceCountSummary = data.ReportData[report.ReportType].Resources
                        .GroupBy(x => x.ResourceType)
                        .ToDictionary(x => x.Key, x => x.Count());

                measureReports.Add(report);
            }

            return new PagedConfigModel<MeasureReportSummary>(measureReports, searchResults.Item2);
        }

        public async Task<List<PatientSubmissionEntry>> FindAsync(Expression<Func<PatientSubmissionEntry, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.FindAsync(predicate, cancellationToken);
        }

        public async Task<PatientSubmissionEntry?> FirstOrDefaultAsync(Expression<Func<PatientSubmissionEntry, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.FirstOrDefaultAsync(predicate, cancellationToken);
        }


        public async Task<PatientSubmissionEntry?> SingleOrDefaultAsync(Expression<Func<PatientSubmissionEntry, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.SingleOrDefaultAsync(predicate, cancellationToken);
        }

        public async Task<PatientSubmissionEntry> SingleAsync(Expression<Func<PatientSubmissionEntry, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.SubmissionEntryRepository.SingleAsync(predicate, cancellationToken);
        }

        public async Task<PatientSubmissionEntry> AddAsync(PatientSubmissionEntry entity, CancellationToken cancellationToken = default)
        {
            var result = await _database.SubmissionEntryRepository.AddAsync(entity, cancellationToken);
            await _database.SaveChangesAsync();
            return result;
        }

        public async Task<PatientSubmissionEntry> UpdateAsync(PatientSubmissionEntryUpdateModel model, CancellationToken cancellationToken = default)
        {
            var entity = await _database.SubmissionEntryRepository.GetAsync(model.Id, cancellationToken);

            var measureReport = model.MeasureReport;

            entity.Status = model.Status;
            entity.ValidationStatus = model.ValidationStatus;
            entity.MeasureReport = model.MeasureReport;
            entity.PayloadUri = model.PayloadUri;
            entity.ModifyDate = DateTime.UtcNow;

            _database.SubmissionEntryRepository.Update(entity);
            await _database.SubmissionEntryRepository.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAllEntriesWithPayloadUri(string reportScheduleId, string payloadUri)
        {
            var date = DateTime.UtcNow;
            var entries = await _database.SubmissionEntryRepository.FindAsync(s => s.ReportScheduleId == reportScheduleId) ?? new List<PatientSubmissionEntry>();

            foreach (var entry in entries)
            {
                entry.PayloadUri = payloadUri;
                entry.ModifyDate = date;
                _database.SubmissionEntryRepository.Update(entry);
            }

            await _database.SaveChangesAsync();
        }

        public async Task UpdateStatusToValidationRequested(string reportScheduleId, string facilityId, string patientId, CancellationToken cancellationToken = default)
        {
            var date = DateTime.UtcNow;
            var entries = await _database.SubmissionEntryRepository.FindAsync(s => s.ReportScheduleId == reportScheduleId && s.FacilityId == facilityId && s.PatientId == patientId, cancellationToken) ?? new List<PatientSubmissionEntry>();

            foreach (var entry in entries)
            {
                entry.Status = PatientSubmissionStatus.ValidationRequested;
                entry.ValidationStatus = ValidationStatus.Requested;
                entry.ModifyDate = date;
                _database.SubmissionEntryRepository.Update(entry);
            }

            await _database.SaveChangesAsync();
        }

        public async Task<PatientSubmissionEntry> AddResourceAsync(PatientSubmissionEntry entry, DomainResource resource, ResourceCategoryType resourceCategoryType, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(resource.Id))
            {
                resource.Id = Guid.NewGuid().ToString();
            }

            var createdResource = await _resourceManager.CreateResourceAsync(entry.FacilityId, entry.ReportScheduleId, entry.Id, [entry.ReportType], resource, entry.PatientId, cancellationToken);

            return entry;
        }
    }
}