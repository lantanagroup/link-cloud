using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Models;

[ExcludeFromCodeCoverage]
public class OrganizationLocationConfigurationSearchParameters
{
    public int? ConfigId { get; set; }
    public bool? IsActive { get; set; }
    public string? DescriptionContains { get; set; }
}
