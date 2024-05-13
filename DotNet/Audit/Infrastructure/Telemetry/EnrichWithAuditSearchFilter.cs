using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using System.Diagnostics;

namespace LantanaGroup.Link.Audit.Infrastructure.Telemetry
{
    public static class AuditActivityExtensions
    {
        public static void EnrichWithAuditSearchFilter(this Activity activity, AuditSearchFilterRecord searchFilter)
        {
            //activity.AddTag(DiagnosticNames.SearchText, searchFilter.SearchText);
            activity.AddTag(DiagnosticNames.FacilityFilter, searchFilter.FilterFacilityBy);
            activity.AddTag(DiagnosticNames.CorrelationFilter, searchFilter.FilterCorrelationBy);
            activity.AddTag(DiagnosticNames.ServiceFilter, searchFilter.FilterServiceBy);
            activity.AddTag(DiagnosticNames.ActionFilter, searchFilter.FilterActionBy);
            activity.AddTag(DiagnosticNames.UserFilter, searchFilter.FilterUserBy);
            activity.AddTag(DiagnosticNames.SortBy, searchFilter.SortBy);
            activity.AddTag(DiagnosticNames.SortOrder, searchFilter.SortOrder);
            activity.AddTag(DiagnosticNames.PageSize, searchFilter.PageSize);
            activity.AddTag(DiagnosticNames.PageNumber, searchFilter.PageNumber);
        }
    }
}
