using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

[ExcludeFromCodeCoverage]
public class CreateOrganizationLocationConfigurationModel
{
    public required string FacilityId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CreateOrganizationLocationConditionModel> Conditions { get; set; } = new();
}