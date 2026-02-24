using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

[ExcludeFromCodeCoverage]
public class OrganizationLocationConfigurationSearchModel
{
    public required string FacilityId { get; set; }
    public int? ConfigId { get; set; }
    public bool? IsActive { get; set; }
    public string? DescriptionContains { get; set; }
}
