using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;

[ExcludeFromCodeCoverage]
public class LocationConfigurationSearchModel
{
    public int? ConfigId { get; set; }
    public string? FacilityId { get; set; }
    public bool? IsActive { get; set; }
    public string? DescriptionContains { get; set; }
}
