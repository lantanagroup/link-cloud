using System.Linq.Expressions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;

public interface IOrganizationLocationMappingQueries
{
    Task<OrganizationLocationMappingModel> GetByIdAsync(int locationMappingId);
    Task<OrganizationLocationMappingModel?> GetByFacilityIdAndLocationIdAsync(string facilityId, string locationId);
    Task<List<OrganizationLocationMappingModel>> GetByFacilityIdAsync(string facilityId);
    Task<PagedConfigModel<OrganizationLocationMappingModel>> SearchAsync(
        OrganizationLocationMappingSearchModel search,
        int pageNumber = 1,
        int pageSize = 10,
        string? sortBy = null,
        SortOrder? sortOrder = null);

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
            .SingleAsync();
    }

    public async Task<OrganizationLocationMappingModel?> GetByFacilityIdAndLocationIdAsync(string facilityId, string locationId)
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
        int pageSize = 10,
        string? sortBy = null,
        SortOrder? sortOrder = null)
    {
        if (string.IsNullOrWhiteSpace(search.FacilityId))
            throw new ArgumentException("FacilityId is required for search.");

        var query = _context.OrganizationLocationMappings
            .Where(m => m.FacilityId == search.FacilityId);

        if (!string.IsNullOrEmpty(search.LocationId))
            query = query.Where(m => m.LocationId == search.LocationId);

        // Free-text (partial) matches on the searchable name fields.
        if (!string.IsNullOrWhiteSpace(search.LocationName))
            query = query.Where(m => m.LocationName != null && m.LocationName.Contains(search.LocationName));

        if (!string.IsNullOrWhiteSpace(search.LocationAlias))
            query = query.Where(m => m.LocationAlias != null && m.LocationAlias.Contains(search.LocationAlias));

        if (!string.IsNullOrWhiteSpace(search.PartOfValue))
            query = query.Where(m => m.PartOfValue != null && m.PartOfValue.Contains(search.PartOfValue));

        if (search.IsOrgLocation.HasValue)
            query = query.Where(m => m.IsOrgLocation == search.IsOrgLocation.Value);

        if (search.IsActive.HasValue)
            query = query.Where(m => m.IsActive == search.IsActive.Value);

        int totalCount = await query.CountAsync();

        var items = await ApplySort(query, sortBy, sortOrder)
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

    // Ordering is restricted to scalar columns; the controller validates sortBy against its allow-list.
    // An unrecognized (or absent) sort field falls back to newest-first (CreateDate descending), which
    // preserves the endpoint's previous default ordering. Direction defaults to descending.
    private static IOrderedQueryable<OrganizationLocationMapping> ApplySort(
        IQueryable<OrganizationLocationMapping> query,
        string? sortBy,
        SortOrder? sortOrder)
    {
        var descending = (sortOrder ?? SortOrder.Descending) == SortOrder.Descending;

        return sortBy switch
        {
            nameof(OrganizationLocationMapping.LocationMappingId) => Order(query, m => m.LocationMappingId, descending),
            nameof(OrganizationLocationMapping.LocationId) => Order(query, m => m.LocationId, descending),
            nameof(OrganizationLocationMapping.LocationName) => Order(query, m => m.LocationName, descending),
            nameof(OrganizationLocationMapping.LocationAlias) => Order(query, m => m.LocationAlias, descending),
            nameof(OrganizationLocationMapping.PartOfValue) => Order(query, m => m.PartOfValue, descending),
            nameof(OrganizationLocationMapping.PartOfId) => Order(query, m => m.PartOfId, descending),
            nameof(OrganizationLocationMapping.IsOrgLocation) => Order(query, m => m.IsOrgLocation, descending),
            nameof(OrganizationLocationMapping.IsActive) => Order(query, m => m.IsActive, descending),
            nameof(OrganizationLocationMapping.ModifiedDate) => Order(query, m => m.ModifiedDate, descending),
            _ => Order(query, m => m.CreateDate, descending)
        };
    }

    private static IOrderedQueryable<OrganizationLocationMapping> Order<TKey>(
        IQueryable<OrganizationLocationMapping> query,
        Expression<Func<OrganizationLocationMapping, TKey>> keySelector,
        bool descending)
        => descending ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);

    #region Hierarchy Methods

    private async Task<Dictionary<string, OrganizationLocationMappingModel>> LoadAllFacilityMappingsAsync(string facilityId)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentException("FacilityId is required.");

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
            .ToDictionaryAsync(m => m.LocationId!);
    }

    private Dictionary<string, List<LocationHierarchyNode>> BuildChildrenLookup(
        Dictionary<string, OrganizationLocationMappingModel> allMappings)
    {
        var lookup = new Dictionary<string, List<LocationHierarchyNode>>(allMappings.Count);

        foreach (var mapping in allMappings.Values)
        {
            if (string.IsNullOrEmpty(mapping.PartOfValue))
                continue;

            if (!lookup.TryGetValue(mapping.PartOfValue, out var children))
            {
                children = new List<LocationHierarchyNode>();
                lookup[mapping.PartOfValue] = children;
            }

            children.Add(new LocationHierarchyNode
            {
                Mapping = mapping,
                Depth = 0 
            });
        }

        return lookup;
    }

    private void AttachChildrenRecursive(LocationHierarchyNode parent,
        Dictionary<string, List<LocationHierarchyNode>> childrenLookup,
        int depth)
    {
        if (!childrenLookup.TryGetValue(parent.Mapping.LocationId!, out var childNodes))
            return;

        // Sort children by name for consistent, predictable tree order
        foreach (var child in childNodes.OrderBy(c => c.Mapping.LocationName))
        {
            child.Depth = depth;
            parent.Children.Add(child);
            AttachChildrenRecursive(child, childrenLookup, depth + 1);
        }
    }

    public async Task<List<LocationHierarchyNode>> GetHierarchyUpAsync(string facilityId, string locationId)
    {
        var allMappings = await LoadAllFacilityMappingsAsync(facilityId);

        if (!allMappings.TryGetValue(locationId, out var current))
            return new List<LocationHierarchyNode>();

        var path = new List<LocationHierarchyNode>();
        var visited = new HashSet<string>();

        while (current != null && visited.Add(current.LocationId!))
        {
            path.Add(new LocationHierarchyNode
            {
                Mapping = current,
                Depth = path.Count 
            });

            if (string.IsNullOrEmpty(current.PartOfValue) ||
                !allMappings.TryGetValue(current.PartOfValue, out current))
            {
                break;
            }
        }

        path.Reverse();

        for (int i = 0; i < path.Count; i++)
        {
            path[i].Depth = i;
        }

        return path;
    }

    public async Task<LocationHierarchyNode> GetFullSubtreeAsync(string facilityId, string locationId)
    {
        var allMappings = await LoadAllFacilityMappingsAsync(facilityId);

        if (!allMappings.TryGetValue(locationId, out _))
            return null!;

        // Walk up to find the true root of this location's hierarchy
        string currentId = locationId;
        var visited = new HashSet<string>();

        while (visited.Add(currentId))
        {
            if (!allMappings.TryGetValue(currentId, out var mapping) ||
                string.IsNullOrEmpty(mapping.PartOfValue) ||
                !allMappings.ContainsKey(mapping.PartOfValue))
            {
                break;
            }
            currentId = mapping.PartOfValue;
        }

        if (!allMappings.TryGetValue(currentId, out var rootMapping))
            return null!;

        var rootNode = new LocationHierarchyNode
        {
            Mapping = rootMapping,
            Depth = 0
        };

        var childrenLookup = BuildChildrenLookup(allMappings);
        AttachChildrenRecursive(rootNode, childrenLookup, 1);

        return rootNode;
    }

    #endregion
}