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

    /// <summary>Active facilities with <c>IsTest</c> set. A missing stored field counts as false.</summary>
    public int Test { get; set; }
}

/// <summary>One row of the facility list, including the test flag. The id/name dictionary stays for older callers.</summary>
public sealed class FacilitySummary
{
    public string FacilityId { get; set; } = "";
    public string? FacilityName { get; set; }
    public bool IsTest { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>Which of a facility id set are test facilities. One call for the ids on a page.</summary>
public sealed class FacilityFlagRequest
{
    public List<string>? FacilityIds { get; set; }
}

public sealed class FacilityFlag
{
    public string FacilityId { get; set; } = "";
    public bool IsTest { get; set; }
}
