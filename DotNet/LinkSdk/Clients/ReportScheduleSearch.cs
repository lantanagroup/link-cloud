using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Sdk.Clients;

/// <summary>
/// Query for <c>GET /api/schedules/search</c>. Null filters are omitted.
/// </summary>
public sealed class ReportScheduleSearch
{
    public string? FacilityId { get; init; }
    public Frequency? Frequency { get; init; }
    public string? ReportType { get; init; }
    public DateTime? ReportStartDate { get; init; }
    public DateTime? ReportEndDate { get; init; }
    public IReadOnlyList<ScheduleStatus>? Statuses { get; init; }
    public bool IncludeDeleted { get; init; }
    public string? SortBy { get; init; }
    public SortOrder? SortOrder { get; init; }
    public int PageSize { get; init; } = 10;
    public int PageNumber { get; init; } = 1;
    public DateOnly? CreateDate { get; init; }
    public Guid? Id { get; init; }
}
