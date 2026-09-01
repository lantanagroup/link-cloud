using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using LinqKit;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public interface IReportScheduledManager
    {
        Task<ReportScheduleModel?> GetReportSchedule(string facilityid, Guid reportId, CancellationToken cancellationToken = default);

        Task<ReportScheduleModel> UpdateAsync(ReportScheduleModel model, CancellationToken cancellationToken);

        Task<ReportScheduleModel> AddAsync(ReportScheduleModel model, CancellationToken cancellationToken);

        Task<List<ReportScheduleModel>> FindAsync(Expression<Func<ReportSchedule, bool>> predicate, CancellationToken cancellationToken = default);

        Task<ReportScheduleModel?> SingleOrDefaultAsync(Expression<Func<ReportSchedule, bool>> predicate, CancellationToken cancellationToken = default);

        Task<ReportScheduleModel> SingleAsync(Expression<Func<ReportSchedule, bool>> predicate, CancellationToken cancellationToken = default);

        Task<PagedConfigModel<ReportScheduleModel>> SearchAsync(
            string? facilityId, Frequency? frequency, string? reportType,
            DateTime? reportStartDate, DateTime? reportEndDate,
            ScheduleStatus[]? statuses, bool? endOfReportPeriodJobHasRun,
            bool includeDeleted, string? sortBy, SortOrder? sortOrder,
            int pageSize, int pageNumber, CancellationToken cancellationToken = default,
            DateOnly? createDate = null, Guid? id = null);

        Task UpdateReportsDeletedStatusForFacility(
            string facilityId, bool deleted, CancellationToken cancellationToken = default);

        Task SoftDeleteByReportTrackingIdAsync(Guid reportTrackingId, CancellationToken cancellationToken = default);
        Task RestoreByReportTrackingIdAsync(Guid reportTrackingId, CancellationToken cancellationToken = default);
        Task<PagedConfigModel<ReportSummaryApiModel>> GetReportSummaries(
            string? facilityId, ReportStatus? status, string? sortBy, SortOrder? sortOrder,
            int pageSize, int pageNumber, CancellationToken cancellationToken = default);

        Task<ReportSummaryApiModel?> GetReportSummary(string reportScheduleId, CancellationToken cancellationToken = default);
    }

    public class ReportScheduledManager : IReportScheduledManager
    {
        private const int BatchSize = 100;

        private readonly ReportDbContext _context;
        private readonly ILogger<ReportScheduledManager> _logger;
        private readonly IQuartzJobHelper _quartzJobHelper;
        private readonly IReportPopulationManager _reportPopulationManager;

        public ReportScheduledManager(
            ReportDbContext context,
            ILogger<ReportScheduledManager> logger,
            IQuartzJobHelper quartzJobHelper,
            IReportPopulationManager reportPopulationManager)
        {
            _context = context;
            _logger = logger;
            _quartzJobHelper = quartzJobHelper;
            _reportPopulationManager = reportPopulationManager;
        }

        public async Task<ReportScheduleModel?> GetReportSchedule(string facilityid, Guid reportId, CancellationToken cancellationToken = default)
        {
            return await _context.ReportSchedule
                .Where(r => r.FacilityId == facilityid && r.Id == reportId && (!r.IsDeleted.HasValue || r.IsDeleted == false))
                .Select(r => new ReportScheduleModel
                {
                    Id = r.Id,
                    CreateDate = r.CreateDate,
                    ModifyDate = r.ModifyDate,
                    FacilityId = r.FacilityId,
                    ReportStartDate = r.ReportStartDate,
                    ReportEndDate = r.ReportEndDate,
                    SubmitReportDateTime = r.SubmitReportDateTime,
                    EnableSubmission = r.EnableSubmission,
                    EndOfReportPeriodJobHasRun = r.EndOfReportPeriodJobHasRun,
                    AdHocType = r.AdHocType,
                    Frequency = r.Frequency,
                    PayloadRootUri = r.PayloadRootUri,
                    Status = r.Status,
                    IsDeleted = r.IsDeleted,
                    ReportTypes = r.ReportTypes.Select(rt => rt.ReportType).ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<List<ReportScheduleModel>> FindAsync(Expression<Func<ReportSchedule, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _context.ReportSchedule
                .Where(predicate)
                .Select(r => new ReportScheduleModel
                {
                    Id = r.Id,
                    CreateDate = r.CreateDate,
                    ModifyDate = r.ModifyDate,
                    FacilityId = r.FacilityId,
                    ReportStartDate = r.ReportStartDate,
                    ReportEndDate = r.ReportEndDate,
                    SubmitReportDateTime = r.SubmitReportDateTime,
                    EnableSubmission = r.EnableSubmission,
                    EndOfReportPeriodJobHasRun = r.EndOfReportPeriodJobHasRun,
                    AdHocType = r.AdHocType,
                    Frequency = r.Frequency,
                    PayloadRootUri = r.PayloadRootUri,
                    Status = r.Status,
                    IsDeleted = r.IsDeleted,
                    ReportTypes = r.ReportTypes.Select(rt => rt.ReportType).ToList()
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<ReportScheduleModel> SingleAsync(Expression<Func<ReportSchedule, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await SingleOrDefaultAsync(predicate, cancellationToken)
                ?? throw new InvalidOperationException("No ReportSchedule found matching the specified criteria.");
        }

        public async Task<ReportScheduleModel?> SingleOrDefaultAsync(Expression<Func<ReportSchedule, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _context.ReportSchedule
                .Where(predicate)
                .Select(r => new ReportScheduleModel
                {
                    Id = r.Id,
                    CreateDate = r.CreateDate,
                    ModifyDate = r.ModifyDate,
                    FacilityId = r.FacilityId,
                    ReportStartDate = r.ReportStartDate,
                    ReportEndDate = r.ReportEndDate,
                    SubmitReportDateTime = r.SubmitReportDateTime,
                    EnableSubmission = r.EnableSubmission,
                    EndOfReportPeriodJobHasRun = r.EndOfReportPeriodJobHasRun,
                    AdHocType = r.AdHocType,
                    Frequency = r.Frequency,
                    PayloadRootUri = r.PayloadRootUri,
                    Status = r.Status,
                    IsDeleted = r.IsDeleted,
                    ReportTypes = r.ReportTypes.Select(rt => rt.ReportType).ToList()
                })
                .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<ReportScheduleModel> UpdateAsync(ReportScheduleModel model, CancellationToken cancellationToken)
        {
            var entity = await _context.ReportSchedule
                .Include(r => r.ReportTypes)
                .FirstOrDefaultAsync(r => r.Id == model.Id, cancellationToken);

            if (entity == null)
                throw new InvalidOperationException($"ReportSchedule with Id {model.Id} not found");

            entity.ModifyDate = DateTime.UtcNow;
            entity.FacilityId = model.FacilityId;
            entity.ReportStartDate = model.ReportStartDate;
            entity.ReportEndDate = model.ReportEndDate;
            entity.SubmitReportDateTime = model.SubmitReportDateTime;
            entity.EnableSubmission = model.EnableSubmission;
            entity.EndOfReportPeriodJobHasRun = model.EndOfReportPeriodJobHasRun;
            entity.AdHocType = model.AdHocType;
            entity.Frequency = model.Frequency;
            entity.PayloadRootUri = model.PayloadRootUri;
            entity.Status = model.Status;
            entity.IsDeleted = model.IsDeleted;

            var existingTypes = entity.ReportTypes.ToList();

            foreach (var type in model.ReportTypes)
            {
                if (!existingTypes.Any(t => t.ReportType == type))
                {
                    entity.ReportTypes.Add(new ScheduleReportType { ReportType = type });
                }
            }

            foreach (var orphan in existingTypes.Where(t => !model.ReportTypes.Contains(t.ReportType)))
            {
                entity.ReportTypes.Remove(orphan);
            }

            _context.Update(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return model;
        }

        public async Task<ReportScheduleModel> AddAsync(ReportScheduleModel model, CancellationToken cancellationToken)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var entity = new ReportSchedule
                {
                    Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id,
                    CreateDate = DateTime.UtcNow,
                    FacilityId = model.FacilityId,
                    ReportStartDate = model.ReportStartDate,
                    ReportEndDate = model.ReportEndDate,
                    SubmitReportDateTime = model.SubmitReportDateTime,
                    EnableSubmission = model.EnableSubmission,
                    EndOfReportPeriodJobHasRun = model.EndOfReportPeriodJobHasRun,
                    AdHocType = model.AdHocType,
                    Frequency = model.Frequency,
                    PayloadRootUri = model.PayloadRootUri,
                    Status = model.Status,
                    IsDeleted = model.IsDeleted
                };

                foreach (var type in model.ReportTypes)
                {
                    entity.ReportTypes.Add(new ScheduleReportType { ReportType = type });
                }

                await _context.AddAsync(entity, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                await _reportPopulationManager.AddWithReportScheduleAsync(model, cancellationToken);

                await transaction.CommitAsync();

                return await SingleAsync(r => r.Id == entity.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding new ReportSchedule");
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<PagedConfigModel<ReportScheduleModel>> SearchAsync(
            string? facilityId, Frequency? frequency, string? reportType,
            DateTime? reportStartDate, DateTime? reportEndDate,
            ScheduleStatus[]? statuses, bool? endOfReportPeriodJobHasRun,
            bool includeDeleted, string? sortBy, SortOrder? sortOrder,
            int pageSize, int pageNumber, CancellationToken cancellationToken = default,
            DateOnly? createDate = null, Guid? id = null)
        {
            Expression<Func<ReportSchedule, bool>> predicate = x => true;

            if (id.HasValue)
                predicate = predicate.And(q => q.Id == id.Value);

            if (!string.IsNullOrWhiteSpace(facilityId))
                predicate = predicate.And(q => q.FacilityId == facilityId);

            if (frequency.HasValue)
                predicate = predicate.And(q => q.Frequency == frequency.Value);

            if (!string.IsNullOrWhiteSpace(reportType))
                predicate = predicate.And(q => q.ReportTypes.Any(r => r.ReportType == reportType));

            if (reportStartDate.HasValue)
                predicate = predicate.And(q => q.ReportStartDate >= reportStartDate.Value);

            if (reportEndDate.HasValue)
                predicate = predicate.And(q => q.ReportEndDate <= reportEndDate.Value);

            if (createDate.HasValue)
            {
                var dayStart = createDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var dayEnd = dayStart.AddDays(1);
                predicate = predicate.And(q => q.CreateDate >= dayStart && q.CreateDate < dayEnd);
            }

            if (statuses != null && statuses.Length > 0)
                predicate = predicate.And(q => statuses.Contains(q.Status));

            if (endOfReportPeriodJobHasRun.HasValue)
                predicate = predicate.And(q => q.EndOfReportPeriodJobHasRun == endOfReportPeriodJobHasRun.Value);

            if (!includeDeleted)
                predicate = predicate.And(q => !q.IsDeleted.HasValue || q.IsDeleted == false);

            var query = _context.ReportSchedule
                .Where(predicate)
                .Select(r => new ReportScheduleModel
                {
                    Id = r.Id,
                    CreateDate = r.CreateDate,
                    ModifyDate = r.ModifyDate,
                    FacilityId = r.FacilityId,
                    ReportStartDate = r.ReportStartDate,
                    ReportEndDate = r.ReportEndDate,
                    SubmitReportDateTime = r.SubmitReportDateTime,
                    EnableSubmission = r.EnableSubmission,
                    EndOfReportPeriodJobHasRun = r.EndOfReportPeriodJobHasRun,
                    AdHocType = r.AdHocType,
                    Frequency = r.Frequency,
                    PayloadRootUri = r.PayloadRootUri,
                    Status = r.Status,
                    IsDeleted = r.IsDeleted,
                    ReportTypes = r.ReportTypes.Select(rt => rt.ReportType).ToList()
                });

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                query = sortBy.ToLower() switch
                {
                    "createdate" => sortOrder == SortOrder.Descending ? query.OrderByDescending(r => r.CreateDate) : query.OrderBy(r => r.CreateDate),
                    "facilityid" => sortOrder == SortOrder.Descending ? query.OrderByDescending(r => r.FacilityId) : query.OrderBy(r => r.FacilityId),
                    "reportstartdate" => sortOrder == SortOrder.Descending ? query.OrderByDescending(r => r.ReportStartDate) : query.OrderBy(r => r.ReportStartDate),
                    "reportenddate" => sortOrder == SortOrder.Descending ? query.OrderByDescending(r => r.ReportEndDate) : query.OrderBy(r => r.ReportEndDate),
                    "status" => sortOrder == SortOrder.Descending ? query.OrderByDescending(r => r.Status) : query.OrderBy(r => r.Status),
                    _ => query.OrderByDescending(r => r.CreateDate)
                };
            }
            else
            {
                query = query.OrderByDescending(r => r.CreateDate);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var results = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var metadata = new PaginationMetadata(pageSize, pageNumber, totalCount);

            return new PagedConfigModel<ReportScheduleModel>(results, metadata);
        }

        public async Task UpdateReportsDeletedStatusForFacility(
            string facilityId,
            bool deleted,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
                throw new ArgumentException("facilityId is required", nameof(facilityId));

            var now = DateTime.UtcNow;
            var quartzFailedIds = new HashSet<Guid>();

            // Quartz handling for Scheduled reports
            var scheduledReports = await _context.ReportSchedule
                .Where(r => r.FacilityId == facilityId && r.Status == ScheduleStatus.Scheduled)
                .ToListAsync(cancellationToken);

            foreach (var r in scheduledReports)
            {
                try
                {
                    if (deleted)
                    {
                        await _quartzJobHelper.DeleteJob(r.Id.ToString(), ReportConstants.MeasureReportSubmissionScheduler.Group, cancellationToken);
                    }
                    else if (r.ReportEndDate > DateTime.UtcNow)
                    {
                        await _quartzJobHelper.ScheduleJob<EndOfReportPeriodJob>(
                            new Dictionary<string, object>
                            {
                                { "ReportScheduleId", r.Id },
                                { "FacilityId", r.FacilityId }
                            },
                            r.ReportEndDate,
                            r.Id.ToString(),
                            ReportConstants.MeasureReportSubmissionScheduler.Group,
                            $"{r.Id}-{r.ReportEndDate}",
                            cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning("Skipping Quartz job re-schedule for report schedule {ReportScheduleId}: ReportEndDate {ReportEndDate} is in the past", r.Id.SanitizeForLog(), r.ReportEndDate);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to {Action} Quartz job for report schedule {ReportScheduleId}",
                        deleted ? "delete" : "re-schedule", r.Id.SanitizeForLog());
                    quartzFailedIds.Add(r.Id);
                }
            }

            if (quartzFailedIds.Count > 0)
                throw new InvalidOperationException(
                    $"Failed to {(deleted ? "delete" : "reschedule")} Quartz jobs for {quartzFailedIds.Count} report schedule(s) for facility {facilityId}.");

            // Update Scheduled reports
            var scheduledToUpdate = scheduledReports
                .Where(r => deleted ? !r.IsDeleted.HasValue || r.IsDeleted == false : r.IsDeleted == true)
                .ToList();

            foreach (var r in scheduledToUpdate)
            {
                r.IsDeleted = deleted;
                r.ModifyDate = now;
            }

            if (scheduledToUpdate.Count > 0)
            {
                _context.ReportSchedule.UpdateRange(scheduledToUpdate);
                await _context.SaveChangesAsync(cancellationToken);
            }

            // Batching for Submitted reports
            List<ReportSchedule> batch;
            do
            {
                batch = await _context.ReportSchedule
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

                _context.ReportSchedule.UpdateRange(batch);
                await _context.SaveChangesAsync(cancellationToken);
            }
            while (batch.Count == BatchSize);
        }

        public async Task SoftDeleteByReportTrackingIdAsync(Guid reportTrackingId, CancellationToken cancellationToken = default)
        {
            var entity = await _context.ReportSchedule
                .FirstOrDefaultAsync(r => r.Id == reportTrackingId && (!r.IsDeleted.HasValue || r.IsDeleted == false), cancellationToken);

            if (entity == null)
                throw new InvalidOperationException($"Report schedule with ID '{reportTrackingId}' not found.");

            if (entity.Status == ScheduleStatus.New || entity.Status == ScheduleStatus.EndOfPeriod)
                throw new InvalidOperationException($"Report schedule '{reportTrackingId}' is currently in progress and cannot be deleted.");

            if (entity.Status == ScheduleStatus.Scheduled)
            {
                try
                {
                    await _quartzJobHelper.DeleteJob(entity.Id.ToString(), ReportConstants.MeasureReportSubmissionScheduler.Group, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete Quartz job for report schedule {ReportScheduleId}", entity.Id.SanitizeForLog());
                    throw new InvalidOperationException($"Failed to delete scheduled job for report schedule '{reportTrackingId}'.");
                }
            }

            entity.IsDeleted = true;
            entity.ModifyDate = DateTime.UtcNow;
            _context.ReportSchedule.Update(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task RestoreByReportTrackingIdAsync(Guid reportTrackingId, CancellationToken cancellationToken = default)
        {
            var entity = await _context.ReportSchedule
                .FirstOrDefaultAsync(r => r.Id == reportTrackingId && r.IsDeleted == true, cancellationToken);

            if (entity == null)
                throw new InvalidOperationException($"Soft-deleted report schedule with ID '{reportTrackingId}' not found.");

            if (entity.Status == ScheduleStatus.Scheduled)
            {
                if (entity.ReportEndDate > DateTime.UtcNow)
                {
                    try
                    {
                        await _quartzJobHelper.ScheduleJob<EndOfReportPeriodJob>(
                            new Dictionary<string, object>
                            {
                                { "ReportScheduleId", entity.Id },
                                { "FacilityId", entity.FacilityId }
                            },
                            entity.ReportEndDate,
                            entity.Id.ToString(),
                            ReportConstants.MeasureReportSubmissionScheduler.Group,
                            $"{entity.Id}-{entity.ReportEndDate}",
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to re-schedule Quartz job for report schedule {ReportScheduleId}", entity.Id.SanitizeForLog());
                        throw new InvalidOperationException($"Failed to re-schedule job for report schedule '{reportTrackingId}'.");
                    }
                }
                else
                {
                    _logger.LogWarning("Skipping Quartz job re-schedule for report schedule {ReportScheduleId}: ReportEndDate {ReportEndDate} is in the past", entity.Id.SanitizeForLog(), entity.ReportEndDate);
                }
            }

            entity.IsDeleted = false;
            entity.ModifyDate = DateTime.UtcNow;
            _context.ReportSchedule.Update(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }

            public async Task<PagedConfigModel<ReportSummaryApiModel>> GetReportSummaries(
                string? facilityId,
                ReportStatus? status,
                string? sortBy,
                SortOrder? sortOrder,
                int pageSize,
                int pageNumber,
                CancellationToken cancellationToken = default)
        {
                var query = CreateReportSummaryQuery();

                if (!string.IsNullOrWhiteSpace(facilityId))
                    query = query.Where(summary => summary.FacilityId == facilityId);

                if (status.HasValue)
                    query = query.Where(summary => summary.Status == status.Value);

                query = sortBy?.ToLower() switch
                {
                    "facilityid" => sortOrder == SortOrder.Descending ? query.OrderByDescending(summary => summary.FacilityId) : query.OrderBy(summary => summary.FacilityId),
                    "reportstartdate" => sortOrder == SortOrder.Descending ? query.OrderByDescending(summary => summary.ReportStartDate) : query.OrderBy(summary => summary.ReportStartDate),
                    "reportenddate" => sortOrder == SortOrder.Descending ? query.OrderByDescending(summary => summary.ReportEndDate) : query.OrderBy(summary => summary.ReportEndDate),
                    "reporttype" => sortOrder == SortOrder.Descending ? query.OrderByDescending(summary => summary.ReportTypes.FirstOrDefault()) : query.OrderBy(summary => summary.ReportTypes.FirstOrDefault()),
                    "status" => sortOrder == SortOrder.Descending ? query.OrderByDescending(summary => summary.Status) : query.OrderBy(summary => summary.Status),
                    "patientcount" => sortOrder == SortOrder.Descending ? query.OrderByDescending(summary => summary.PatientCount) : query.OrderBy(summary => summary.PatientCount),
                    "initialpopulationcount" => sortOrder == SortOrder.Descending ? query.OrderByDescending(summary => summary.InitialPopulationCount) : query.OrderBy(summary => summary.InitialPopulationCount),
                    "lastupdateddate" => sortOrder == SortOrder.Descending ? query.OrderByDescending(summary => summary.LastUpdatedDate) : query.OrderBy(summary => summary.LastUpdatedDate),
                    _ => query.OrderByDescending(summary => summary.LastUpdatedDate)
                };

                var totalCount = await query.CountAsync(cancellationToken);
                var results = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);

                return new PagedConfigModel<ReportSummaryApiModel>(
                    results,
                    new PaginationMetadata(pageSize, pageNumber, totalCount));
        }

        public async Task<ReportSummaryApiModel?> GetReportSummary(
            string reportScheduleId,
            CancellationToken cancellationToken = default)
        {
            if (!Guid.TryParse(reportScheduleId, out var parsedReportScheduleId))
                return null;

            return await CreateReportSummaryQuery()
                .FirstOrDefaultAsync(summary => summary.ReportScheduleId == parsedReportScheduleId.ToString(), cancellationToken);
            }

            private IQueryable<ReportSummaryApiModel> CreateReportSummaryQuery()
            {
                    return _context.ReportSchedule
                        .Select(reportSchedule => new ReportSummaryApiModel
                    {
                            ReportScheduleId = reportSchedule.Id.ToString(),
                            FacilityId = reportSchedule.FacilityId,
                            ReportStartDate = reportSchedule.ReportStartDate,
                            ReportEndDate = reportSchedule.ReportEndDate,
                            ReportTypes = reportSchedule.ReportTypes.Select(reportType => reportType.ReportType).ToList(),
                            Status = reportSchedule.IsDeleted == true
                            ? ReportStatus.Canceled
                                : reportSchedule.Status == ScheduleStatus.Submitted
                                ? ReportStatus.Completed
                                    : reportSchedule.Status == ScheduleStatus.New ||
                                      reportSchedule.Status == ScheduleStatus.Scheduled ||
                                      reportSchedule.Status == ScheduleStatus.EndOfPeriod
                                    ? ReportStatus.Pending
                                    : ReportStatus.Unknown,
                            PatientCount = reportSchedule.ReportEntries.Count(),
                            InitialPopulationCount = reportSchedule.ReportPopulations
                                .SelectMany(reportPopulation => reportPopulation.GroupPopulations)
                            .Where(groupPopulation => groupPopulation.PopulationId == "initial-population")
                            .SelectMany(groupPopulation => groupPopulation.MeasureReportPopulations)
                            .Count(),
                            LastUpdatedDate = reportSchedule.ModifyDate ?? reportSchedule.CreateDate
                    });
        }
    }
}