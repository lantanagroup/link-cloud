using AngleSharp.Dom;
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
        Task<ReportEntryStatusModel?> GetPatientEntry(string reportId, string reportType, string patientId, CancellationToken cancellationToken = default);

        Task<ReportEntryStatusModel> UpdateAsync(ReportEntryStatusModel entry,
            CancellationToken cancellationToken);

        Task<ReportEntryStatusModel> AddAsync(ReportEntryStatusModel entry,
            CancellationToken cancellationToken);

        Task<List<ReportEntryStatusModel>> FindAsync(Expression<Func<ReportEntryStatusModel, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<ReportEntryStatusModel?> SingleOrDefaultAsync(
            Expression<Func<ReportEntryStatusModel, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task UpdateStatusToValidationRequested(string reportScheduleId, string patientId, CancellationToken cancellationToken = default);
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

        public async Task<ReportEntryStatusModel?> GetPatientEntry(string reportId, string reportType, string patientId, CancellationToken cancellationToken = default)
        {
            return (await _database.ReportEntryStatusRepository.FindAsync(r => r.ReportScheduleId == reportId && r.PatientId == patientId && r.ReportType == reportType, cancellationToken)).SingleOrDefault();
        }

        public async Task<ReportEntryStatusModel?> SingleOrDefaultAsync(Expression<Func<ReportEntryStatusModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportEntryStatusRepository.SingleOrDefaultAsync(predicate, cancellationToken);
        }

        public async Task<ReportEntryStatusModel> UpdateAsync(ReportEntryStatusModel entry, CancellationToken cancellationToken)
        {
            return await _database.ReportEntryStatusRepository.UpdateAsync(entry, cancellationToken);
        }

        public async Task UpdateStatusToValidationRequested(string reportScheduleId, string patientId, CancellationToken cancellationToken = default)
        {
            var entries = await _database.ReportEntryStatusRepository.FindAsync(s => s.ReportScheduleId == reportScheduleId && s.PatientId == patientId, cancellationToken) ?? new();

            foreach (var entry in entries)
            {
                entry.Status = PatientSubmissionStatus.ValidationRequested;
                entry.ValidationStatus = ValidationStatus.Requested;
                await _database.ReportEntryStatusRepository.UpdateAsync(entry, cancellationToken);
            }
        }
    }
}
    