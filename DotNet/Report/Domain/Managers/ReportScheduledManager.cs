using System.Linq.Expressions;
using LantanaGroup.Link.Report.Entities;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public interface IReportScheduledManager
    {
        Task<ReportScheduleModel?> GetReportSchedule(string facilityId, DateTime startDate,
            DateTime endDate, string reportType, CancellationToken cancellationToken = default);

        Task<List<ReportScheduleModel>?> GetReportSchedules(string facilityId, DateTime startDate,
            DateTime endDate, CancellationToken cancellationToken = default);

        Task<ReportScheduleModel> UpdateAsync(ReportScheduleModel schedule,
            CancellationToken cancellationToken);

        Task<ReportScheduleModel> AddAsync(ReportScheduleModel schedule,
            CancellationToken cancellationToken);

        Task<List<ReportScheduleModel>> FindAsync(Expression<Func<ReportScheduleModel, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<ReportScheduleModel?> SingleOrDefaultAsync(
            Expression<Func<ReportScheduleModel, bool>> predicate,
            CancellationToken cancellationToken = default);
    }


    public class ReportScheduledManager : IReportScheduledManager
    {
        private readonly IDatabase _database;

        public ReportScheduledManager(IDatabase database)
        {
            _database = database;
        }

        public async Task<ReportScheduleModel?> GetReportSchedule(string facilityId, DateTime startDate, DateTime endDate, string reportType, CancellationToken cancellationToken = default)
        {
            // find existing report scheduled for this facility, report type, and date range
            return (await _database.ReportScheduledRepository.FindAsync(
                r => r.FacilityId == facilityId && r.ReportStartDate == startDate && r.ReportEndDate == endDate &&
                     r.ReportTypes.Contains(reportType), cancellationToken))?.SingleOrDefault();
        }

        public async Task<List<ReportScheduleModel>?> GetReportSchedules(string facilityId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            // find existing report scheduled for this facility, report type, and date range
            return (await _database.ReportScheduledRepository.FindAsync(
                r => r.FacilityId == facilityId && r.ReportStartDate == startDate && r.ReportEndDate == endDate, cancellationToken))?.ToList();
        }

        public async Task<ReportScheduleModel?> SingleOrDefaultAsync(Expression<Func<ReportScheduleModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportScheduledRepository.SingleOrDefaultAsync(predicate, cancellationToken);
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
