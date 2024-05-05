using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.Account.Application.Models.User
{
    public record UserSearchFilterRecord
    {
        [PiiData]
        public string? SearchText { get; init; }
        public string? FilterFacilityBy { get; init; }
        public string? FilterRoleBy { get; init; }
        public string? FilterClaimBy { get; init; }
        public bool IncludeDeactivatedUsers { get; init; }
        public bool IncludeDeletedUsers { get; init; }
        public string? SortBy { get; init; }
        public SortOrder SortOrder { get; init; }
        public int PageSize { get; init; }
        public int PageNumber { get; init; }
    }
}
