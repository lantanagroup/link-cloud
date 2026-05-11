using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LinqKit;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public interface IReportResourceManager
    {
        Task<ReportResourceModel> UpdateAsync(ReportResourceModel model, CancellationToken cancellationToken);

        Task<ReportResourceModel> AddAsync(ReportResourceModel model, CancellationToken cancellationToken);

        Task AddAsyncWithAggregateResult(string facilityId, Guid reportId, string patientId, AggregateResult aggregateResult, CancellationToken cancellationToken);

        Task<List<ReportResourceModel>> FindAsync(Expression<Func<ReportResource, bool>> predicate, CancellationToken cancellationToken = default);

        Task<ReportResourceModel?> SingleOrDefaultAsync(Expression<Func<ReportResource, bool>> predicate, CancellationToken cancellationToken = default);

        Task<PagedConfigModel<ReportResourceModel>> SearchAsync(
            string? facilityId, Guid? reportScheduleId, string? patientId,
            string? measureReportId, string? resourceType, string? resourceId,
            string? sortBy, SortOrder? sortOrder,
            int pageSize, int pageNumber, CancellationToken cancellationToken = default);
    }

    public class ReportResourceManager : IReportResourceManager
    {
        private readonly ReportDbContext _context;

        public ReportResourceManager(ReportDbContext context)
        {
            _context = context;
        }

        public async Task<ReportResourceModel> UpdateAsync(ReportResourceModel model, CancellationToken cancellationToken)
        {
            var entity = await _context.ReportResource
                .FirstOrDefaultAsync(r => r.Id == model.Id, cancellationToken);

            if (entity == null)
                throw new InvalidOperationException($"ReportResource with Id {model.Id} not found");

            entity.ModifyDate = DateTime.UtcNow;
            entity.FacilityId = model.FacilityId;
            entity.PatientId = model.PatientId;
            entity.MeasureReportId = model.MeasureReportId;
            entity.ResourceType = model.ResourceType;
            entity.ResourceId = model.ResourceId;

            _context.Update(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return model;
        }

        public async Task<ReportResourceModel> AddAsync(ReportResourceModel model, CancellationToken cancellationToken)
        {
            var entity = new ReportResource
            {
                Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id,
                CreateDate = DateTime.UtcNow,
                FacilityId = model.FacilityId,
                ReportScheduleId = model.ReportScheduleId,
                PatientId = model.PatientId,
                MeasureReportId = model.MeasureReportId,
                ResourceType = model.ResourceType,
                ResourceId = model.ResourceId
            };

            await _context.AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            model.Id = entity.Id;
            return model;
        }

        /// <summary>
        /// Daniel - 04/2026: As of 0.6, writing to the ReportResource table is temporarily disabled for two reasons:
        ///     1. After a few staging runs, we found that Report generated ~13 million entries into the table which could affect write/read performance. Strategies on how to better store this data will need to be investigated.
        ///     2. Currently, the data in this table is not being used in the admin UI. 
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="reportId"></param>
        /// <param name="patientId"></param>
        /// <param name="aggregateResult"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task AddAsyncWithAggregateResult(string facilityId, Guid reportId, string patientId, AggregateResult aggregateResult, CancellationToken cancellationToken)
        {
            if (aggregateResult?.MeasureReportResults == null || !aggregateResult.MeasureReportResults.Any())
                return;

            var entities = new List<ReportResource>();

            foreach (var measureReport in aggregateResult.MeasureReportResults)
            {
                if (measureReport.ResourceReferences == null) continue;

                foreach (var resource in measureReport.ResourceReferences)
                {
                    if (resource == null || resource.Length < 2)
                        continue;

                    var entity = new ReportResource
                    {
                        Id = Guid.NewGuid(),
                        CreateDate = DateTime.UtcNow,
                        FacilityId = facilityId,
                        ReportScheduleId = reportId,
                        PatientId = patientId,
                        MeasureReportId = measureReport.MeasureReportId,
                        ResourceType = resource[0],
                        ResourceId = resource[1]
                    };

                    entities.Add(entity);
                }
            }

            if (entities.Count > 0)
            {
                await _context.ReportResource.AddRangeAsync(entities, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<List<ReportResourceModel>> FindAsync(Expression<Func<ReportResource, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _context.ReportResource
                .Where(predicate)
                .Select(r => new ReportResourceModel
                {
                    Id = r.Id,
                    CreateDate = r.CreateDate,
                    ModifyDate = r.ModifyDate,
                    FacilityId = r.FacilityId,
                    ReportScheduleId = r.ReportScheduleId,
                    PatientId = r.PatientId,
                    MeasureReportId = r.MeasureReportId,
                    ResourceType = r.ResourceType,
                    ResourceId = r.ResourceId
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<ReportResourceModel?> SingleOrDefaultAsync(Expression<Func<ReportResource, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _context.ReportResource
                .Where(predicate)
                .Select(r => new ReportResourceModel
                {
                    Id = r.Id,
                    CreateDate = r.CreateDate,
                    ModifyDate = r.ModifyDate,
                    FacilityId = r.FacilityId,
                    ReportScheduleId = r.ReportScheduleId,
                    PatientId = r.PatientId,
                    MeasureReportId = r.MeasureReportId,
                    ResourceType = r.ResourceType,
                    ResourceId = r.ResourceId
                })
                .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<PagedConfigModel<ReportResourceModel>> SearchAsync(
            string? facilityId, Guid? reportScheduleId, string? patientId,
            string? measureReportId, string? resourceType, string? resourceId,
            string? sortBy, SortOrder? sortOrder,
            int pageSize, int pageNumber, CancellationToken cancellationToken = default)
        {
            Expression<Func<ReportResource, bool>> predicate = x => true;

            if (!string.IsNullOrWhiteSpace(facilityId))
                predicate = predicate.And(q => q.FacilityId == facilityId);

            if (reportScheduleId.HasValue)
                predicate = predicate.And(q => q.ReportScheduleId == reportScheduleId.Value);

            if (!string.IsNullOrWhiteSpace(patientId))
                predicate = predicate.And(q => q.PatientId == patientId);

            if (!string.IsNullOrWhiteSpace(measureReportId))
                predicate = predicate.And(q => q.MeasureReportId == measureReportId);

            if (!string.IsNullOrWhiteSpace(resourceType))
                predicate = predicate.And(q => q.ResourceType == resourceType);

            if (!string.IsNullOrWhiteSpace(resourceId))
                predicate = predicate.And(q => q.ResourceId == resourceId);

            var query = _context.ReportResource
                .Where(predicate)
                .Select(r => new ReportResourceModel
                {
                    Id = r.Id,
                    CreateDate = r.CreateDate,
                    ModifyDate = r.ModifyDate,
                    FacilityId = r.FacilityId,
                    ReportScheduleId = r.ReportScheduleId,
                    PatientId = r.PatientId,
                    MeasureReportId = r.MeasureReportId,
                    ResourceType = r.ResourceType,
                    ResourceId = r.ResourceId
                });

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                query = sortBy.ToLower() switch
                {
                    "createdate" => sortOrder == SortOrder.Descending
                        ? query.OrderByDescending(r => r.CreateDate).ThenByDescending(r => r.Id)
                        : query.OrderBy(r => r.CreateDate).ThenBy(r => r.Id),
                    "facilityid" => sortOrder == SortOrder.Descending
                        ? query.OrderByDescending(r => r.FacilityId).ThenByDescending(r => r.Id)
                        : query.OrderBy(r => r.FacilityId).ThenBy(r => r.Id),
                    "patientid" => sortOrder == SortOrder.Descending
                        ? query.OrderByDescending(r => r.PatientId).ThenByDescending(r => r.Id)
                        : query.OrderBy(r => r.PatientId).ThenBy(r => r.Id),
                    "resourcetype" => sortOrder == SortOrder.Descending
                        ? query.OrderByDescending(r => r.ResourceType).ThenByDescending(r => r.Id)
                        : query.OrderBy(r => r.ResourceType).ThenBy(r => r.Id),
                    _ => query.OrderByDescending(r => r.CreateDate).ThenByDescending(r => r.Id)
                };
            }
            else
            {
                query = query.OrderByDescending(r => r.CreateDate).ThenByDescending(r => r.Id);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var results = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var metadata = new PaginationMetadata(pageSize, pageNumber, totalCount);

            return new PagedConfigModel<ReportResourceModel>(results, metadata);
        }
    }
}