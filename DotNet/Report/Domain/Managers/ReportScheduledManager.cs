using LantanaGroup.Link.Report.Application.Factory;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Utilities;
using LinqKit;
using Microsoft.EntityFrameworkCore;
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
        private const int BatchSize = 100;

        private readonly ILogger<ReportScheduledManager> _logger;
        private readonly IDatabase _database;
        private readonly ScheduledReportFactory _scheduledReportFactory;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly MongoDbContext _context;
        private readonly IQuartzJobHelper _quartzJobHelper;

        public ReportScheduledManager(ILogger<ReportScheduledManager> logger, MongoDbContext context, IDatabase database,
            ScheduledReportFactory scheduledReportFactory, IServiceScopeFactory serviceScopeFactory,
            IQuartzJobHelper quartzJobHelper)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context;
            _database = database;
            _scheduledReportFactory = scheduledReportFactory;
            _serviceScopeFactory = serviceScopeFactory;
            _quartzJobHelper = quartzJobHelper ?? throw new ArgumentNullException(nameof(quartzJobHelper));
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

            var now = DateTime.UtcNow;

            // Handle Quartz jobs for Scheduled reports and soft-delete/restore them
            var scheduledReports = await _database.ReportScheduledRepository
                .FindAsync(r => r.FacilityId == facilityId && r.Status == ScheduleStatus.Scheduled, cancellationToken);

            foreach (var r in scheduledReports)
            {
                try
                {
                    if (deleted)
                    {
                        await _quartzJobHelper.DeleteJob(r.Id, ReportConstants.MeasureReportSubmissionScheduler.Group, cancellationToken);
                    }
                    else if (r.ReportEndDate > DateTime.UtcNow)
                    {
                        await _quartzJobHelper.ScheduleJob<EndOfReportPeriodJob>(
                            new Dictionary<string, object>
                            {
                                { "ReportScheduleId", r.Id },
                                { "FacilityId", r.FacilityId }
                            },
                            new DateTimeOffset(r.ReportEndDate, TimeSpan.Zero),
                            r.Id,
                            ReportConstants.MeasureReportSubmissionScheduler.Group,
                            $"{r.Id}-{r.ReportEndDate}",
                            cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning("Skipping Quartz job re-schedule for report schedule {ReportScheduleId}: ReportEndDate {ReportEndDate} is in the past", r.Id, r.ReportEndDate);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to {Action} Quartz job for report schedule {ReportScheduleId}",
                        deleted ? "delete" : "re-schedule", r.Id);
                }
            }

            var scheduledToUpdate = scheduledReports
                .Where(r => deleted ? !r.IsDeleted.HasValue || r.IsDeleted == false : r.IsDeleted == true)
                .ToList();

            if (scheduledToUpdate.Count > 0)
            {
                foreach (var r in scheduledToUpdate)
                {
                    r.IsDeleted = deleted;
                    r.ModifyDate = now;
                }
                _context.ReportSchedules.UpdateRange(scheduledToUpdate);
                await _context.SaveChangesAsync(cancellationToken);
            }

            // Soft-delete (or restore) Submitted reports in batches
            List<ReportSchedule> batch;

            do
            {
                batch = await _context.ReportSchedules
                    .Where(r => r.FacilityId == facilityId &&
                                r.Status == ScheduleStatus.Submitted &&
                                (deleted
                                    ? !r.IsDeleted.HasValue || r.IsDeleted == false
                                    : r.IsDeleted == true))
                    .Take(BatchSize)
                    .ToListAsync(cancellationToken);

                if (batch.Count == 0) break;

                foreach (var r in batch)
                {
                    r.IsDeleted = deleted;
                    r.ModifyDate = now;
                }

                _context.ReportSchedules.UpdateRange(batch);
                await _context.SaveChangesAsync(cancellationToken);
            }
            while (batch.Count == BatchSize);
        }
    }
}
