using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;

public interface IOrganizationLocationConfigurationQueries
{
    Task<OrganizationLocationConfigurationModel> GetByIdAsync(int configId);
    Task<List<OrganizationLocationConfigurationModel>> GetByFacilityIdAsync(string facilityId);
    Task<bool> HasActiveByFacilityIdAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<PagedConfigModel<OrganizationLocationConfigurationModel>> SearchAsync(
        OrganizationLocationConfigurationSearchModel search,
        int pageNumber = 1,
        int pageSize = 10);
}

public class OrganizationLocationConfigurationQueries : IOrganizationLocationConfigurationQueries
{
    private readonly DataAcquisitionDbContext _context;

    public OrganizationLocationConfigurationQueries(DataAcquisitionDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<OrganizationLocationConfigurationModel> GetByIdAsync(int configId)
    {
        return await _context.LocationConfigurations
            .Where(c => c.ConfigId == configId)
            .Select(c => new OrganizationLocationConfigurationModel
            {
                ConfigId = c.ConfigId,
                FacilityId = c.FacilityId,
                Description = c.Description,
                IsActive = c.IsActive,
                CreatedOn = c.CreatedOn ?? DateTime.UtcNow,
                ModifiedOn = c.ModifiedOn ?? DateTime.UtcNow,
                Conditions = c.LocationConditions.Select(cond => new OrganizationLocationConditionModel
                {
                    ConditionId = cond.ConditionId,
                    FhirPath = cond.FhirPath,
                    Priority = cond.Priority,
                    CreatedOn = cond.CreatedOn ?? DateTime.UtcNow,
                    ModifiedOn = cond.ModifiedOn ?? DateTime.UtcNow
                }).ToList()
            })
            .SingleAsync();
    }

    public async Task<List<OrganizationLocationConfigurationModel>> GetByFacilityIdAsync(string facilityId)
    {
        return await _context.LocationConfigurations
            .Where(c => c.FacilityId == facilityId)
            .Select(c => new OrganizationLocationConfigurationModel
            {
                ConfigId = c.ConfigId,
                FacilityId = c.FacilityId,
                Description = c.Description,
                IsActive = c.IsActive,
                CreatedOn = c.CreatedOn ?? DateTime.UtcNow,
                ModifiedOn = c.ModifiedOn ?? DateTime.UtcNow,
                Conditions = c.LocationConditions.Select(cond => new OrganizationLocationConditionModel
                {
                    ConditionId = cond.ConditionId,
                    FhirPath = cond.FhirPath,
                    Priority = cond.Priority,
                    CreatedOn = cond.CreatedOn ?? DateTime.UtcNow,
                    ModifiedOn = cond.ModifiedOn ?? DateTime.UtcNow
                }).ToList()
            })
            .ToListAsync();
    }

    public async Task<bool> HasActiveByFacilityIdAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        return await _context.LocationConfigurations
            .AnyAsync(c => c.FacilityId == facilityId && c.IsActive, cancellationToken);
    }

    public async Task<PagedConfigModel<OrganizationLocationConfigurationModel>> SearchAsync(
        OrganizationLocationConfigurationSearchModel search,
        int pageNumber = 1,
        int pageSize = 10)
    {
        var query = _context.LocationConfigurations.Where(c => c.FacilityId == search.FacilityId).AsQueryable();

        if (search.ConfigId.HasValue)
            query = query.Where(c => c.ConfigId == search.ConfigId.Value);

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
            .Select(c => new OrganizationLocationConfigurationModel
            {
                ConfigId = c.ConfigId,
                FacilityId = c.FacilityId,
                Description = c.Description,
                IsActive = c.IsActive,
                CreatedOn = c.CreatedOn ?? DateTime.UtcNow,
                ModifiedOn = c.ModifiedOn ?? DateTime.UtcNow,
                Conditions = c.LocationConditions.Select(cond => new OrganizationLocationConditionModel
                {
                    ConditionId = cond.ConditionId,
                    FhirPath = cond.FhirPath,
                    Priority = cond.Priority,
                    CreatedOn = cond.CreatedOn ?? DateTime.UtcNow,
                    ModifiedOn = cond.ModifiedOn ?? DateTime.UtcNow
                }).ToList()
            })
            .ToListAsync();

        return new PagedConfigModel<OrganizationLocationConfigurationModel>(items, new PaginationMetadata(pageSize, pageNumber, totalCount));
    }
}
