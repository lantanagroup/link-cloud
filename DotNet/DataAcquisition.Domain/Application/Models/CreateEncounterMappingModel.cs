using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

[ExcludeFromCodeCoverage]
public class CreateEncounterMappingModel
{
    [Required(AllowEmptyStrings = false)]
    public required string FacilityId { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string PatientId { get; set; }

    [Required(AllowEmptyStrings = false)] 
    public required string EncounterId { get; set; }
    
    public bool MappedToOrg { get; set; }
    public List<int>? OrganizationLocationMappingIds { get; set; }
}
