using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;

public interface ILocationConfigurationQueries
{
    Task<LocationConfigurationModel> GetByIdAsync(int configId);
    Task<LocationConfigurationModel> GetByFacilityIdAsync(int facilityId);
    Task<PagedConfigModel<LocationConfigurationModel>> SearchAsync(
        LocationConfigurationSearchModel search,
        int pageNumber = 1,
        int pageSize = 10);
}

public class LocationConfigurationQueries : ILocationConfigurationQueries
{
    private readonly DataAcquisitionDbContext _context;

    public LocationConfigurationQueries(DataAcquisitionDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<LocationConfigurationModel> GetByIdAsync(int configId)
    {
        return await _context.LocationConfigurations
            .Where(c => c.ConfigId == configId)
            .Select(c => new LocationConfigurationModel
            {
                ConfigId = c.ConfigId,
                FacilityId = c.FacilityId,
                Description = c.Description,
                IsActive = c.IsActive ?? false,
                CreatedOn = c.CreatedOn ?? DateTime.UtcNow,
                ModifiedOn = c.ModifiedOn ?? DateTime.UtcNow,
                Conditions = c.LocationConditions.Select(cond => new LocationConditionModel
                {
                    ConditionId = cond.ConditionId,
                    FhirPath = cond.FhirPath,
                    Priority = cond.Priority ?? 1,
                    CreatedOn = cond.CreatedOn ?? DateTime.UtcNow,
                    ModifiedOn = cond.ModifiedOn ?? DateTime.UtcNow
                }).ToList()
            })
            .FirstOrDefaultAsync();
    }

    public async Task<LocationConfigurationModel> GetByFacilityIdAsync(int facilityId)
    {
        return await _context.LocationConfigurations
            .Where(c => c.FacilityId == facilityId)
            .Select(c => new LocationConfigurationModel
            {
                ConfigId = c.ConfigId,
                FacilityId = c.FacilityId,
                Description = c.Description,
                IsActive = c.IsActive ?? false,
                CreatedOn = c.CreatedOn ?? DateTime.UtcNow,
                ModifiedOn = c.ModifiedOn ?? DateTime.UtcNow,
                Conditions = c.LocationConditions.Select(cond => new LocationConditionModel
                {
                    ConditionId = cond.ConditionId,
                    FhirPath = cond.FhirPath,
                    Priority = cond.Priority ?? 1,
                    CreatedOn = cond.CreatedOn ?? DateTime.UtcNow,
                    ModifiedOn = cond.ModifiedOn ?? DateTime.UtcNow
                }).ToList()
            })
            .FirstOrDefaultAsync();
    }

    public async Task<PagedConfigModel<LocationConfigurationModel>> SearchAsync(
        LocationConfigurationSearchModel search,
        int pageNumber = 1,
        int pageSize = 10)
    {
        var query = _context.LocationConfigurations.AsQueryable();

        if (search.ConfigId.HasValue)
            query = query.Where(c => c.ConfigId == search.ConfigId.Value);

        if (search.FacilityId.HasValue)
            query = query.Where(c => c.FacilityId == search.FacilityId.Value);

        if (search.IsActive.HasValue)
            query = query.Where(c => c.IsActive == search.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(search.DescriptionContains))
            query = query.Where(c => c.Description.Contains(search.DescriptionContains));

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(c => c.FacilityId)
            .ThenBy(c => c.ConfigId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new LocationConfigurationModel
            {
                ConfigId = c.ConfigId,
                FacilityId = c.FacilityId,
                Description = c.Description,
                IsActive = c.IsActive ?? false,
                CreatedOn = c.CreatedOn ?? DateTime.UtcNow,
                ModifiedOn = c.ModifiedOn ?? DateTime.UtcNow,
                Conditions = c.LocationConditions.Select(cond => new LocationConditionModel
                {
                    ConditionId = cond.ConditionId,
                    FhirPath = cond.FhirPath,
                    Priority = cond.Priority ?? 1,
                    CreatedOn = cond.CreatedOn ?? DateTime.UtcNow,
                    ModifiedOn = cond.ModifiedOn ?? DateTime.UtcNow
                }).ToList()
            })
            .ToListAsync();

        return new PagedConfigModel<LocationConfigurationModel>(items, new PaginationMetadata(pageSize, pageNumber, totalCount));
    }
}