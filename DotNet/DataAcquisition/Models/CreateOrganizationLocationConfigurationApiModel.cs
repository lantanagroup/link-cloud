using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Models;

[ExcludeFromCodeCoverage]
public class CreateOrganizationLocationConfigurationApiModel
{
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CreateOrganizationLocationConditionModel> Conditions { get; set; } = new();
}
