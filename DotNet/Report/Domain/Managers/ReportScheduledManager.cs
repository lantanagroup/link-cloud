using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LinqKit;
using System.Linq.Expressions;

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

        Task<PagedConfigModel<ReportSchedule>> SearchAsync(
            string? facilityId,
            Frequency? frequency,
            string? reportType,
            DateTime? reportStartDate,
            DateTime? reportEndDate,
            ScheduleStatus? status,
            bool? endOfReportPeriodJobHasRun,
            bool includeDeleted,
            string? sortBy,
            SortOrder? sortOrder,
            int pageSize,
            int pageNumber,
            CancellationToken cancellationToken = default);
        
        Task UpdateReportsDeletedStatusForFacility(
            string facilityId,
            bool deleted,  
            CancellationToken cancellationToken = default);

    }


    public class ReportScheduledManager : IReportScheduledManager
    {
        private readonly IDatabase _database;
        private readonly MongoDbContext _context;

        public ReportScheduledManager(MongoDbContext context, IDatabase database)
        {
            _context = context;
            _database = database;
        }

        public async Task<ReportSchedule?> GetReportSchedule(string facilityid, string reportId,
            CancellationToken cancellationToken = default)
        {
            // find existing report scheduled for this facility, report type, and date range
            return (await _database.ReportScheduledRepository.FindAsync(
                r => r.FacilityId == facilityid && r.Id == reportId, cancellationToken))?.SingleOrDefault();
        }

        public async Task<ReportSchedule?> SingleOrDefaultAsync(Expression<Func<ReportSchedule, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await _database.ReportScheduledRepository.SingleOrDefaultAsync(predicate, cancellationToken);
        }

        public async Task<List<ReportSchedule>> FindAsync(Expression<Func<ReportSchedule, bool>> predicate,
            CancellationToken cancellationToken = default)
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
        

        public async Task<PagedConfigModel<ReportSchedule>> SearchAsync(
            string? facilityId,
            Frequency? frequency,
            string? reportType,
            DateTime? reportStartDate,
            DateTime? reportEndDate,
            ScheduleStatus? status,
            bool? endOfReportPeriodJobHasRun,
            bool includeDeleted,
            string? sortBy,
            SortOrder? sortOrder,
            int pageSize,
            int pageNumber,
            CancellationToken cancellationToken = default)
        {
            Expression<Func<ReportSchedule, bool>> predicate = x => true;

            if (!string.IsNullOrWhiteSpace(facilityId))
            {
                predicate = predicate.And(q => q.FacilityId == facilityId);
            }

            if (frequency.HasValue)
            {
                predicate = predicate.And(q => q.Frequency == frequency.Value);
            }

            if (!string.IsNullOrWhiteSpace(reportType))
            {
                predicate = predicate.And(q => q.ReportTypes.Contains(reportType));
            }

            if (reportStartDate.HasValue)
            {
                predicate = predicate.And(q => q.ReportStartDate >= reportStartDate.Value);
            }

            if (reportEndDate.HasValue)
            {
                predicate = predicate.And(q => q.ReportEndDate <= reportEndDate.Value);
            }

            if (status.HasValue)
            {
                predicate = predicate.And(q => q.Status == status.Value);
            }

            if (endOfReportPeriodJobHasRun.HasValue)
            {
                predicate = predicate.And(q => q.EndOfReportPeriodJobHasRun == endOfReportPeriodJobHasRun.Value);
            }
            
            if (!includeDeleted)
            {
                predicate = predicate.And(q => !q.IsDeleted.HasValue || q.IsDeleted == false);
            }

            var (results, metadata) = await _database.ReportScheduledRepository.SearchAsync(
                predicate,
                sortBy,
                sortOrder,
                pageSize,
                pageNumber,
                cancellationToken);

            return new PagedConfigModel<ReportSchedule>(results, metadata);
        }
        
        public async Task UpdateReportsDeletedStatusForFacility(
            string facilityId,
            bool deleted,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
                throw new ArgumentException("facilityId is required", nameof(facilityId));

            var reports = await _database.ReportScheduledRepository
                .FindAsync(r => r.FacilityId == facilityId, cancellationToken);

            var now = DateTime.UtcNow;

            foreach (var r in reports)
            {
                r.IsDeleted = deleted;
                r.ModifyDate = now;
            }

            _context.ReportSchedules.UpdateRange(reports);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
