using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;

namespace LantanaGroup.Link.Account.Application.Interfaces.Persistence
{
    public interface ISearchRepository
    {
        Task<(IEnumerable<LinkUser>, IPaginationMetadata)> SearchAsync(string? searchText, string? filterFacilityBy, string? filterRoleBy, string? filterClaimBy, bool isActive, bool isDeleted, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default);
    }
}
