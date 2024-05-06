using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.Account.Application.Interfaces.Factories.User
{
    public interface IUserSearchFilterRecordFactory
    {
        UserSearchFilterRecord Create(string? searchText, string? filterFacilityBy, string? filterRoleBy, string? filterClaimBy,
            bool includeDeactivatedUsers, bool includeDeletedUsers, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber);
    }
}
