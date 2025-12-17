using AngleSharp.Dom;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Factory;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public interface IReportEntryStatusManager 
    {
        Task<ReportEntryStatusModel?> GetEntry(string reportScheduleId, string patientId, CancellationToken cancellationToken = default);

        Task<ReportEntryStatusModel> UpdateAsync(ReportEntryStatusModel entry,
            CancellationToken cancellationToken);

        Task<ReportEntryStatusModel> AddAsync(ReportEntryStatusModel entry,
            CancellationToken cancellationToken);

        Task<List<ReportEntryStatusModel>> FindAsync(Expression<Func<ReportEntryStatusModel, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<ReportEntryStatusModel?> SingleOrDefaultAsync(
            Expression<Func<ReportEntryStatusModel, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<PatientReportSummary> GetPatients(string facilityId, string reportId, int page, int count, CancellationToken cancellationToken = default);

        //Task UpdateStatusToValidationRequested(string reportScheduleId, string patientId, CancellationToken cancellationToken = default);
    }

    public class ReportEntryStatusManager : IReportEntryStatusManager
    {
        private readonly IDatabase _database;

        public ReportEntryStatusManager(IDatabase database)
        {
            _database = database;
        }

        public async Task<ReportEntryStatusModel> AddAsync(ReportEntryStatusModel entry, CancellationToken cancellationToken)
        {
            return await _database.ReportEntryStatusRepository.AddAsync(entry, cancellationToken);
        }

        public async Task<List<ReportEntryStatusModel>> FindAsync(Expression<Func<ReportEntryStatusModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportEntryStatusRepository.FindAsync(predicate, cancellationToken);
        }

        public async Task<ReportEntryStatusModel?> GetEntry(string reportScheduleId, string patientId, CancellationToken cancellationToken = default)
        {
            return (await _database.ReportEntryStatusRepository.FindAsync(r => r.ReportScheduleId == reportScheduleId && r.PatientId == patientId, cancellationToken)).SingleOrDefault();
        }

        public async Task<ReportEntryStatusModel?> SingleOrDefaultAsync(Expression<Func<ReportEntryStatusModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportEntryStatusRepository.SingleOrDefaultAsync(predicate, cancellationToken);
        }

        public async Task<ReportEntryStatusModel> UpdateAsync(ReportEntryStatusModel entry, CancellationToken cancellationToken)
        {
            return await _database.ReportEntryStatusRepository.UpdateAsync(entry, cancellationToken);
        }

        public async Task<PatientReportSummary> GetPatients(string facilityId, string reportId, int page, int count, CancellationToken cancellationToken = default)
        {
            var scheduledReport = await _database.ReportScheduledRepository.SingleOrDefaultAsync(x => x.FacilityId == facilityId && x.Id == reportId, cancellationToken);

            if (scheduledReport is null) throw new ArgumentNullException($"Scheduled report with ID {reportId} not found.");

            var measureReportEntries = await _database.ReportEntryStatusRepository.FindAsync(x => x.ReportScheduleId == reportId, cancellationToken);

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
    }
}
    