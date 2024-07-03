using System.Linq.Expressions;
using LantanaGroup.Link.Report.Entities;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public interface IMeasureReportScheduledManager
    {
        Task<MeasureReportScheduleModel?> GetMeasureReportSchedule(string facilityId, DateTime startDate,
            DateTime endDate, string reportType, CancellationToken cancellationToken = default);

        Task<List<MeasureReportScheduleModel>?> GetMeasureReportSchedules(string facilityId, DateTime startDate,
            DateTime endDate, CancellationToken cancellationToken = default);

        Task<MeasureReportScheduleModel> UpdateAsync(MeasureReportScheduleModel schedule,
            CancellationToken cancellationToken);

        Task<MeasureReportScheduleModel> AddAsync(MeasureReportScheduleModel schedule,
            CancellationToken cancellationToken);

        Task<List<MeasureReportScheduleModel>> FindAsync(Expression<Func<MeasureReportScheduleModel, bool>> predicate,
            CancellationToken cancellationToken = default);
    }


    public class MeasureReportScheduledManager : IMeasureReportScheduledManager
    {
        private readonly IDatabase _database;

        public MeasureReportScheduledManager(IDatabase database)
        {
            _database = database;
        }

        public async Task<MeasureReportScheduleModel?> GetMeasureReportSchedule(string facilityId, DateTime startDate, DateTime endDate, string reportType, CancellationToken cancellationToken = default)
        {
            // find existing report scheduled for this facility, report type, and date range
            return (await _database.ReportScheduledRepository.FindAsync(
                r => r.FacilityId == facilityId && r.ReportStartDate == startDate && r.ReportEndDate == endDate &&
                     r.ReportType == reportType, cancellationToken))?.SingleOrDefault();
        }

        public async Task<List<MeasureReportScheduleModel>?> GetMeasureReportSchedules(string facilityId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            // find existing report scheduled for this facility, report type, and date range
            return (await _database.ReportScheduledRepository.FindAsync(
                r => r.FacilityId == facilityId && r.ReportStartDate == startDate && r.ReportEndDate == endDate, cancellationToken))?.ToList();
        }

        public async Task<List<MeasureReportScheduleModel>> FindAsync(Expression<Func<MeasureReportScheduleModel, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportScheduledRepository.FindAsync(predicate, cancellationToken);
        }

        public async Task<MeasureReportScheduleModel> UpdateAsync(MeasureReportScheduleModel schedule, CancellationToken cancellationToken)
        {
            return await _database.ReportScheduledRepository.UpdateAsync(schedule, cancellationToken);
        }

        public async Task<MeasureReportScheduleModel> AddAsync(MeasureReportScheduleModel schedule, CancellationToken cancellationToken)
        {
            return await _database.ReportScheduledRepository.AddAsync(schedule, cancellationToken);
        }
    }
}
