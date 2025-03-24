using System.Linq.Expressions;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;

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
    }


    public class ReportScheduledManager : IReportScheduledManager
    {
        private readonly IDatabase _database;

        public ReportScheduledManager(IDatabase database)
        {
            _database = database;
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
    }
}
