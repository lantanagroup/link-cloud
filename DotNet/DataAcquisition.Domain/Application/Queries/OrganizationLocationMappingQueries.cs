using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;

public interface IOrganizationLocationMappingQueries
{
    Task<OrganizationLocationMappingModel> GetByIdAsync(int locationMappingId);
    Task<OrganizationLocationMappingModel> GetByFacilityIdAndLocationIdAsync(string facilityId, string locationId);
    Task<List<OrganizationLocationMappingModel>> GetByFacilityIdAsync(string facilityId);
    Task<PagedConfigModel<OrganizationLocationMappingModel>> SearchAsync(
        OrganizationLocationMappingSearchModel search,
        int pageNumber = 1,
        int pageSize = 10);

    Task<List<LocationHierarchyNode>> GetHierarchyUpAsync(string facilityId, string locationId);
    Task<LocationHierarchyNode> GetFullSubtreeAsync(string facilityId, string locationId);
}

public class OrganizationLocationMappingQueries : IOrganizationLocationMappingQueries
{
    private readonly DataAcquisitionDbContext _context;

    public OrganizationLocationMappingQueries(DataAcquisitionDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<OrganizationLocationMappingModel> GetByIdAsync(int locationMappingId)
    {
        return await _context.OrganizationLocationMappings
            .Where(m => m.LocationMappingId == locationMappingId)
            .Select(m => new OrganizationLocationMappingModel
            {
                LocationMappingId = m.LocationMappingId,
                FacilityId = m.FacilityId,
                LocationId = m.LocationId,
                LocationName = m.LocationName,
                LocationAlias = m.LocationAlias,
                PartOfValue = m.PartOfValue,
                PartOfId = m.PartOfId,
                IsOrgLocation = m.IsOrgLocation,
                CreateDate = m.CreateDate,
                ModifiedDate = m.ModifiedDate,
                IsActive = m.IsActive
            })
            .FirstOrDefaultAsync();
    }

    public async Task<OrganizationLocationMappingModel> GetByFacilityIdAndLocationIdAsync(string facilityId, string locationId)
    {
        return await _context.OrganizationLocationMappings
            .Where(m => m.FacilityId == facilityId && m.LocationId == locationId)
            .Select(m => new OrganizationLocationMappingModel
            {
                LocationMappingId = m.LocationMappingId,
                FacilityId = m.FacilityId,
                LocationId = m.LocationId,
                LocationName = m.LocationName,
                LocationAlias = m.LocationAlias,
                PartOfValue = m.PartOfValue,
                PartOfId = m.PartOfId,
                IsOrgLocation = m.IsOrgLocation,
                CreateDate = m.CreateDate,
                ModifiedDate = m.ModifiedDate,
                IsActive = m.IsActive
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<OrganizationLocationMappingModel>> GetByFacilityIdAsync(string facilityId)
    {
        return await _context.OrganizationLocationMappings
            .Where(m => m.FacilityId == facilityId)
            .Select(m => new OrganizationLocationMappingModel
            {
                LocationMappingId = m.LocationMappingId,
                FacilityId = m.FacilityId,
                LocationId = m.LocationId,
                LocationName = m.LocationName,
                LocationAlias = m.LocationAlias,
                PartOfValue = m.PartOfValue,
                PartOfId = m.PartOfId,
                IsOrgLocation = m.IsOrgLocation,
                CreateDate = m.CreateDate,
                ModifiedDate = m.ModifiedDate,
                IsActive = m.IsActive
            })
            .OrderBy(m => m.LocationName)
            .ToListAsync();
    }

    public async Task<PagedConfigModel<OrganizationLocationMappingModel>> SearchAsync(
        OrganizationLocationMappingSearchModel search,
        int pageNumber = 1,
        int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(search.FacilityId))
            throw new ArgumentException("FacilityId is required for search.");

        var query = _context.OrganizationLocationMappings
            .Where(m => m.FacilityId == search.FacilityId);

        if (!string.IsNullOrEmpty(search.LocationId))
            query = query.Where(m => m.LocationId == search.LocationId);

        if (search.IsOrgLocation.HasValue)
            query = query.Where(m => m.IsOrgLocation == search.IsOrgLocation.Value);

        if (search.IsActive.HasValue)
            query = query.Where(m => m.IsActive == search.IsActive.Value);

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(m => m.CreateDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new OrganizationLocationMappingModel
            {
                LocationMappingId = m.LocationMappingId,
                FacilityId = m.FacilityId,
                LocationId = m.LocationId,
                LocationName = m.LocationName,
                LocationAlias = m.LocationAlias,
                PartOfValue = m.PartOfValue,
                PartOfId = m.PartOfId,
                IsOrgLocation = m.IsOrgLocation,
                CreateDate = m.CreateDate,
                ModifiedDate = m.ModifiedDate,
                IsActive = m.IsActive
            })
            .ToListAsync();

        return new PagedConfigModel<OrganizationLocationMappingModel>(items, new PaginationMetadata(pageSize, pageNumber, totalCount));
    }

    public async Task<List<LocationHierarchyNode>> GetHierarchyUpAsync(string facilityId, string locationId)
    {
        var path = new List<LocationHierarchyNode>();
        var visited = new HashSet<string>();

        string currentId = locationId;

        while (!string.IsNullOrEmpty(currentId) && !visited.Contains(currentId))
        {
            visited.Add(currentId);

            var mapping = await GetByFacilityIdAndLocationIdAsync(facilityId, currentId);
            if (mapping == null) break;

            path.Add(new LocationHierarchyNode
            {
                Mapping = mapping,
                Depth = path.Count
            });

            currentId = mapping.PartOfValue;
        }

        path.Reverse();
        return path;
    }

    public async Task<LocationHierarchyNode> GetFullSubtreeAsync(string facilityId, string locationId)
    {
        var hierarchyUp = await GetHierarchyUpAsync(facilityId, locationId);
        if (hierarchyUp.Count == 0) return null;

        var rootMapping = hierarchyUp[0].Mapping;

        var rootNode = new LocationHierarchyNode
        {
            Mapping = rootMapping,
            Depth = 0
        };

        await BuildSubtreeRecursiveAsync(rootNode, facilityId);
        return rootNode;
    }

    private async Task BuildSubtreeRecursiveAsync(LocationHierarchyNode node, string facilityId)
    {
        var children = await _context.OrganizationLocationMappings
            .Where(m => m.FacilityId == facilityId && m.PartOfValue == node.Mapping.LocationId)
            .Select(m => new OrganizationLocationMappingModel
            {
                LocationMappingId = m.LocationMappingId,
                FacilityId = m.FacilityId,
                LocationId = m.LocationId,
                LocationName = m.LocationName,
                LocationAlias = m.LocationAlias,
                PartOfValue = m.PartOfValue,
                PartOfId = m.PartOfId,
                IsOrgLocation = m.IsOrgLocation,
                CreateDate = m.CreateDate,
                ModifiedDate = m.ModifiedDate,
                IsActive = m.IsActive
            })
            .ToListAsync();

        foreach (var child in children)
        {
            var childNode = new LocationHierarchyNode
            {
                Mapping = child,
                Depth = node.Depth + 1
            };

            node.Children.Add(childNode);
            await BuildSubtreeRecursiveAsync(childNode, facilityId);
        }
    }
}