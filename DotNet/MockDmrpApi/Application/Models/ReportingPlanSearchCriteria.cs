using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.MockDmrpApi.Application.Models;

/// <summary>
/// Fields a reporting plan entry search may be sorted by.
/// </summary>
/// <remarks>
/// This is a closed set on purpose. The shared repository resolves the sort field by
/// building an <c>Expression.Property</c> from the supplied name, which throws for any
/// name that is not a property -- a server fault produced by client input. Restricting
/// the input to an enum means only these six names can ever reach it.
/// </remarks>
public enum ReportingPlanSortBy
{
    FacilityId,
    Measure,
    ReportingMonth,
    ReportingYear,
    CreateDate,
    ModifyDate
}

/// <summary>
/// Filters and paging for a reporting plan entry search. Internal query shape, not a
/// contract type -- the generated DTOs describe request and response bodies only.
/// </summary>
public class ReportingPlanSearchCriteria
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;

    public string? FacilityId { get; set; }

    /// <summary>Matched case-insensitively.</summary>
    public string? Measure { get; set; }

    public int? ReportingMonth { get; set; }

    public int? ReportingYear { get; set; }

    public string? IsReporting { get; set; }

    public ReportingPlanSortBy SortBy { get; set; } = ReportingPlanSortBy.CreateDate;

    public SortOrder SortOrder { get; set; } = SortOrder.Descending;

    public int PageSize { get; set; } = DefaultPageSize;

    public int PageNumber { get; set; } = 1;
}
