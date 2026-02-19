using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;

[ExcludeFromCodeCoverage]
public class CreateLocationConfigurationModel
{
    public int FacilityId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CreateLocationConditionModel> Conditions { get; set; } = new();
}