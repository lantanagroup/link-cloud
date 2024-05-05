using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;

namespace LantanaGroup.Link.Account.Application.Interfaces.Persistence
{
    public interface ISearchRepository
    {
        Task<(IEnumerable<LinkUser>, IPaginationMetadata)> SearchAsync(string? searchText, string? filterFacilityBy, string? filterRoleBy, string? filterClaimBy, bool includeDeactivatedUsers, bool includeDeletedUsers, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default);

        Task<(IEnumerable<LinkUser>, IPaginationMetadata)> TenantSearchAsync(string filterFacilityBy, string? searchText, string? filterRoleBy, string? filterClaimBy, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default);
    }
}
