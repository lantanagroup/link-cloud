using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Infrastructure.Telemetry
{
    public static class AccountActivityExtensions
    {
        public static void EnrichWithUserSearchFilter(this Activity activity, UserSearchFilterRecord searchFilter)
        {
            activity.AddTag(DiagnosticNames.FacilityFilter, searchFilter.FilterFacilityBy);
            activity.AddTag("role.filter", searchFilter.FilterRoleBy);
            activity.AddTag("claim.filter", searchFilter.FilterClaimBy);
            activity.AddTag("include.deactivated.users", searchFilter.IncludeDeactivatedUsers);
            activity.AddTag("include.deleted.users", searchFilter.IncludeDeletedUsers);
            activity.AddTag(DiagnosticNames.SortBy, searchFilter.SortBy);
            activity.AddTag(DiagnosticNames.SortOrder, searchFilter.SortOrder);
            activity.AddTag(DiagnosticNames.PageSize, searchFilter.PageSize);
            activity.AddTag(DiagnosticNames.PageNumber, searchFilter.PageNumber);

        }
    }
}
