using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

[ExcludeFromCodeCoverage]
public class UpdateOrganizationLocationConfigurationModel
{
    public string? Description { get; set; }
    public bool? IsActive { get; set; }              
    public List<UpdateOrganizationLocationConditionModel>? Conditions { get; set; }
}
