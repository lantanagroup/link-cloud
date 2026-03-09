using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Models;

/// <summary>
/// Dedicated API models for this controller (extract to separate files later if desired).
/// These avoid mixing route parameters with "required" properties on internal domain models.
/// </summary>
[ExcludeFromCodeCoverage]
public class OrganizationLocationConfigurationSearchParameters
{
    public int? ConfigId { get; set; }
    public bool? IsActive { get; set; }
    public string? DescriptionContains { get; set; }
}
