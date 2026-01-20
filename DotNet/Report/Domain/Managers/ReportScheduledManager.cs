using System.Linq.Expressions;
using LantanaGroup.Link.Report.Application.Factory;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public interface IReportScheduledManager
    {
        Task<ReportSchedule?> GetReportSchedule(string facilityid, string reportId, CancellationToken cancellationToken = default);

        Task<ReportSchedule> UpdateAsync(ReportSchedule schedule,
            CancellationToken cancellationToken);

        Task<ReportSchedule> AddAsync(ReportSchedule schedule,
            CancellationToken cancellationToken);

        Task<List<ReportSchedule>> FindAsync(Expression<Func<ReportSchedule, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<ReportSchedule?> SingleOrDefaultAsync(
            Expression<Func<ReportSchedule, bool>> predicate,
            CancellationToken cancellationToken = default);
    }


    public class ReportScheduledManager : IReportScheduledManager
    {
        private readonly IDatabase _database;
        private readonly ScheduledReportFactory _scheduledReportFactory;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ReportScheduledManager(IDatabase database, ScheduledReportFactory scheduledReportFactory, IServiceScopeFactory serviceScopeFactory)
        {
            _database = database;
            _scheduledReportFactory = scheduledReportFactory;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task<ReportSchedule?> GetReportSchedule(string facilityid, string reportId, CancellationToken cancellationToken = default)
        {
            // find existing report scheduled for this facility, report type, and date range
            return (await _database.ReportScheduledRepository.FindAsync(r => r.FacilityId == facilityid && r.Id == reportId, cancellationToken))?.SingleOrDefault();
        }

        public async Task<ReportSchedule?> SingleOrDefaultAsync(Expression<Func<ReportSchedule, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportScheduledRepository.SingleOrDefaultAsync(predicate, cancellationToken);
        }

        public async Task<List<ReportSchedule>> FindAsync(Expression<Func<ReportSchedule, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _database.ReportScheduledRepository.FindAsync(predicate, cancellationToken);
        }

        public async Task<ReportSchedule> UpdateAsync(ReportSchedule schedule, CancellationToken cancellationToken)
        {
            _database.ReportScheduledRepository.Update(schedule);
            await _database.ReportScheduledRepository.SaveChangesAsync(cancellationToken);
            return schedule;
        }

        public async Task<ReportSchedule> AddAsync(ReportSchedule schedule, CancellationToken cancellationToken)
        {
            var entity = await _database.ReportScheduledRepository.AddAsync(schedule, cancellationToken);
            await _database.SaveChangesAsync();
            return entity;
        }
    }
}
