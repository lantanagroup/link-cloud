using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

[ExcludeFromCodeCoverage]
public class CreateEncounterMappingModel
{
    public required string FacilityId { get; set; }
    public required string PatientId { get; set; }
    public required string EncounterId { get; set; }
    public bool MappedToOrg { get; set; }
    public List<int>? OrganizationLocationMappingIds { get; set; }
}
