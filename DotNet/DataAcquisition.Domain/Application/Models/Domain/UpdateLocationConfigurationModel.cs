using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;

[ExcludeFromCodeCoverage]
public class UpdateLocationConfigurationModel
{
    public string Description { get; set; }           // null = no change
    public bool? IsActive { get; set; }               // null = no change
    public List<UpdateLocationConditionModel> Conditions { get; set; } // null = no change, otherwise replace all
}
