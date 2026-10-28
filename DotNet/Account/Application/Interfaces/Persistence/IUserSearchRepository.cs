using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;


namespace LantanaGroup.Link.Account.Application.Interfaces.Persistence
{
    public interface IUserSearchRepository
    {
        Task<(IEnumerable<LinkUser>, PaginationMetadata)> SearchAsync(string? searchText, string? filterFacilityBy, string? filterRoleBy, string? filterClaimBy, bool includeDeactivatedUsers, bool includeDeletedUsers, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default);

        Task<(IEnumerable<LinkUser>, PaginationMetadata)> FacilitySearchAsync(string facilityId, string? searchText, string? filterRoleBy, string? filterClaimBy, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default);
    }
}
