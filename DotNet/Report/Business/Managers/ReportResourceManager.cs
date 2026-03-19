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

        public async Task AddAsyncWithAggregateResult(string facilityId, Guid reportId, string patientId, AggregateResult aggregateResult, CancellationToken cancellationToken)
        {
            foreach (var measureReport in aggregateResult.MeasureReportResults)
            {
                foreach (var resource in measureReport.ResourceReferences)
                {
                    var resModel = new ReportResourceModel
                    {
                        Id = Guid.NewGuid(),
                        ReportScheduleId = reportId,
                        FacilityId = facilityId,
                        PatientId = patientId,
                        MeasureReportId = measureReport.MeasureReportId,
                        ResourceType = resource[0],
                        ResourceId = resource[1],
                        CreateDate = DateTime.UtcNow
                    };

                    await AddAsync(resModel, cancellationToken);
                }
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
                    "createdate" => sortOrder == SortOrder.Descending ? query.OrderByDescending(r => r.CreateDate) : query.OrderBy(r => r.CreateDate),
                    "facilityid" => sortOrder == SortOrder.Descending ? query.OrderByDescending(r => r.FacilityId) : query.OrderBy(r => r.FacilityId),
                    "patientid" => sortOrder == SortOrder.Descending ? query.OrderByDescending(r => r.PatientId) : query.OrderBy(r => r.PatientId),
                    "resourcetype" => sortOrder == SortOrder.Descending ? query.OrderByDescending(r => r.ResourceType) : query.OrderBy(r => r.ResourceType),
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

            return new PagedConfigModel<ReportResourceModel>(results, metadata);
        }
    }
}