using LantanaGroup.Link.Normalization.Application.Models.FacilityLocationMappings;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Domain.Queries;

public interface IFacilityLocationLocalCodeMappingQueries
{
    Task<FacilityLocationLocalCodeMappingModel?> Get(string id);
    Task<PagedConfigModel<FacilityLocationLocalCodeMappingModel>> Search(FacilityLocationLocalCodeMappingSearchModel model);
}

public class FacilityLocationLocalCodeMappingQueries : IFacilityLocationLocalCodeMappingQueries
{
    private readonly NormalizationDbContext _dbContext;

    public FacilityLocationLocalCodeMappingQueries(NormalizationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FacilityLocationLocalCodeMappingModel?> Get(string id)
    {
        return (await Search(new FacilityLocationLocalCodeMappingSearchModel
        {
            Id = id,
            PageSize = 1
        })).Records.SingleOrDefault();
    }

    public async Task<PagedConfigModel<FacilityLocationLocalCodeMappingModel>> Search(FacilityLocationLocalCodeMappingSearchModel model)
    {
        var mappings = _dbContext.FacilityLocationLocalCodeMappings
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(model.Id))
        {
            mappings = mappings.Where(mapping => mapping.Id == model.Id);
        }

        if (!string.IsNullOrWhiteSpace(model.FacilityId))
        {
            mappings = mappings.Where(mapping => mapping.FacilityLocation!.FacilityId == model.FacilityId);
        }

        if (!string.IsNullOrWhiteSpace(model.LocationId))
        {
            mappings = mappings.Where(mapping => mapping.FacilityLocation!.LocationId == model.LocationId);
        }

        if (!string.IsNullOrWhiteSpace(model.LocalCodeSystem))
        {
            mappings = mappings.Where(mapping => mapping.LocalCodeSystem == model.LocalCodeSystem);
        }

        if (!string.IsNullOrWhiteSpace(model.LocalCode))
        {
            mappings = mappings.Where(mapping => mapping.LocalCode == model.LocalCode);
        }

        if (model.HSLOCId.HasValue)
        {
            mappings = mappings.Where(mapping => mapping.HSLOCId == model.HSLOCId.Value);
        }

        if (model.Unmapped.HasValue)
        {
            mappings = model.Unmapped.Value
                ? mappings.Where(mapping => mapping.HSLOCId == null)
                : mappings.Where(mapping => mapping.HSLOCId != null);
        }

        var pageSize = Math.Clamp(model.PageSize ?? 10, 1, 100);
        var pageNumber = Math.Max(model.PageNumber ?? 1, 1);
        var count = await mappings.CountAsync();

        var records = await mappings
            .OrderBy(mapping => mapping.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(mapping => new FacilityLocationLocalCodeMappingModel
            {
                Id = mapping.Id,
                FacilityId = mapping.FacilityLocation!.FacilityId,
                LocationId = mapping.FacilityLocation!.LocationId,
                LocationName = mapping.FacilityLocation!.LocationName,
                LocationAlias = mapping.FacilityLocation!.LocationAlias,
                LocalCodeSystem = mapping.LocalCodeSystem,
                LocalCode = mapping.LocalCode,
                HSLOCId = mapping.HSLOCId,
                HSLOCCode = mapping.HSLOC == null ? null : mapping.HSLOC.HSLOCCode,
                HSLOCVersion = mapping.HSLOC == null ? null : mapping.HSLOC.Version,
                CreateDate = mapping.CreateDate,
                ModifyDate = mapping.ModifyDate
            })
            .ToListAsync();

        return new PagedConfigModel<FacilityLocationLocalCodeMappingModel>(
            records,
            new PaginationMetadata(pageSize, pageNumber, count));
    }
}