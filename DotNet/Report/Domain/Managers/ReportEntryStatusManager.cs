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
    public interface IReportEntryStatusManager 
    {
        Task<ReportEntryModel?> GetEntry(string reportScheduleId, string patientId, CancellationToken cancellationToken = default);

        Task<ReportEntryModel> UpdateAsync(ReportEntryModel entry,
            CancellationToken cancellationToken);

        Task<ReportEntryModel> AddAsync(ReportEntryModel entry,
            CancellationToken cancellationToken);

        Task<List<ReportEntryModel>> FindAsync(Expression<Func<ReportEntryModel, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<ReportEntryModel?> SingleOrDefaultAsync(
            Expression<Func<ReportEntryModel, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<ReportEntryModel> UpdateAsyncWithConsumerResult(MeasureReportGeneratedValue consumerValue, CancellationToken cancellationToken = default);

        Task<ReportEntryModel> UpdateAsyncWithAggregateResult(ReportEntryModel entry, AggregateResult aggregateResult, CancellationToken cancellationToken = default);

        Task<PatientReportSummary> GetPatients(string facilityId, string reportId, int page, int count, CancellationToken cancellationToken = default);

        Task<PagedConfigModel<MeasureReportSummary>> GetMeasureReports(Expression<Func<ReportEntryModel, bool>> predicate, string sortBy, SortOrder sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default);
    }

    public class ReportEntryStatusManager : IReportEntryStatusManager
    {
        private readonly IDatabase _database;
        private readonly MeasureReportSummaryFactory _measureReportSummaryFactory;

        public ReportEntryStatusManager(IDatabase database, MeasureReportSummaryFactory measureReportSummaryFactory)
        {
            _database = database;
            _measureReportSummaryFactory = measureReportSummaryFactory;
        }

        public async Task<ReportEntryModel> AddAsync(ReportEntryModel entry, CancellationToken cancellationToken)
        {
            return await _database.ReportEntryRepository.AddAsync(entry, cancellationToken);
        }

        public async Task<List<ReportEntryModel>> FindAsync(Expression<Func<ReportEntryModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportEntryRepository.FindAsync(predicate, cancellationToken);
        }

        public async Task<ReportEntryModel?> GetEntry(string reportScheduleId, string patientId, CancellationToken cancellationToken = default)
        {
            return (await _database.ReportEntryRepository.FindAsync(r => r.ReportScheduleId == reportScheduleId && r.PatientId == patientId, cancellationToken)).SingleOrDefault();
        }

        public async Task<ReportEntryModel?> SingleOrDefaultAsync(Expression<Func<ReportEntryModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportEntryRepository.SingleOrDefaultAsync(predicate, cancellationToken);
        }

        public async Task<ReportEntryModel> UpdateAsync(ReportEntryModel entry, CancellationToken cancellationToken)
        {
            return await _database.ReportEntryRepository.UpdateAsync(entry, cancellationToken);
        }

        public async Task<ReportEntryModel> UpdateAsyncWithConsumerResult(MeasureReportGeneratedValue consumerValue, CancellationToken cancellationToken = default)
        {
            var reportEntry = await this.GetEntry(consumerValue.ReportTrackingId, consumerValue.PatientId);

            EvaluatedMeasureReport measureReportEntry =  reportEntry.MeasureReportList.First(x => x.ReportType == consumerValue.ReportType);

            measureReportEntry.MeasureReportId = consumerValue.MeasureReportId;
            measureReportEntry.MeasureReportFileName = consumerValue.MeasureReportFileName;
            measureReportEntry.MeasureReportUri = consumerValue.MeasureReportURI;

            if (consumerValue.IsReportable)
            {
                measureReportEntry.Status = MeasureReportStatus.ReadyForValidation;
            }
            else
            {
                measureReportEntry.Status = MeasureReportStatus.NotReportable;
            }

            return await _database.ReportEntryRepository.UpdateAsync(reportEntry, cancellationToken);
        }

        public async Task<ReportEntryModel> UpdateAsyncWithAggregateResult(ReportEntryModel entry, AggregateResult aggregateResult, CancellationToken cancellationToken = default)
        {
            entry.AggregateReportUri = aggregateResult.Uri.AbsoluteUri;
            entry.AggregateReportBlobName = aggregateResult.BlobName;
            entry.ModifyDate = DateTime.UtcNow;

            foreach (var measureReportResult in aggregateResult.MeasureReportResults) {
                entry.MeasureReportList.First(x => x.ReportType == measureReportResult.ReportType).ResourceCount = measureReportResult.ResourceCount;
            }

            return await _database.ReportEntryRepository.UpdateAsync(entry, cancellationToken);
        }

        public async Task<PatientReportSummary> GetPatients(string facilityId, string reportId, int page, int count, CancellationToken cancellationToken = default)
        {
            var scheduledReport = await _database.ReportScheduledRepository.SingleOrDefaultAsync(x => x.FacilityId == facilityId && x.Id == reportId, cancellationToken);

            if (scheduledReport is null) throw new ArgumentNullException($"Scheduled report with ID {reportId} not found.");

            var measureReportEntries = await _database.ReportEntryRepository.FindAsync(x => x.ReportScheduleId == reportId, cancellationToken);

            var patientIds = measureReportEntries.Select(x => x.PatientId).Distinct().ToList();

            var pagedPatients = patientIds.Skip((page - 1) * count).Take(count).ToList();

            var patientSummaries = new List<PatientSummary>();

            foreach (var patientId in pagedPatients)
            {
                try
                {
                    //TODO: Look into how resources are used
                    //var patientResource = (await _database.PatientResourceRepository.FindAsync(r => r.FacilityId == facilityId && r.PatientId == patientId && r.ResourceId == patientId && r.ResourceType == "Patient", cancellationToken)).SingleOrDefault();

                    //if (patientResource?.GetResource() is not Patient patient)
                    //{
                    //    patientSummaries.Add(new PatientSummary { id = patientId, name = string.Empty });
                    //    continue;
                    //}

                    //var name = patient.Name?.FirstOrDefault();
                    //var fullName = name != null ? $"{string.Join(" ", name.Given ?? Enumerable.Empty<string>())} {name.Family}".Trim() : string.Empty;

                    //patientSummaries.Add(new PatientSummary
                    //{
                    //    id = patientId,
                    //    name = fullName
                    //});
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

        public async Task<PagedConfigModel<MeasureReportSummary>> GetMeasureReports(Expression<Func<ReportEntryModel, bool>> predicate, string sortBy, SortOrder sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default)
        {
            //TODO: Refactors are probably needed here to support the new report entry model
            // Get individual measure report entries for this report
            var searchResults = await _database.ReportEntryRepository
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

    }
}
    