namespace LantanaGroup.Link.Shared.Application.Models.Tenant;

/// <summary>
/// Facility aggregates. <see cref="FacilityCounts.Matched"/> is set only when the request
/// included a facility id set, and is how many of that set exist and are not deleted.
/// </summary>
public sealed class FacilityCountRequest
{
    public List<string>? FacilityIds { get; set; }
}

public sealed class FacilityCounts
{
    public int Total { get; set; }
    public int? Matched { get; set; }
}
