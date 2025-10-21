using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Models;

[ExcludeFromCodeCoverage]
public class UpdateEncounterMappingApiModel
{
    [Required]
    public required bool? MappedToOrg { get; set; } = false;
}
