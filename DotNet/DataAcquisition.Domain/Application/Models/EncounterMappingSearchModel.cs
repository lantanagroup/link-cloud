using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

[ExcludeFromCodeCoverage]
public class EncounterMappingSearchModel
{
    public string? FacilityId { get; set; }
    public string? PatientId { get; set; }
    public string? EncounterId { get; set; }
    public bool? MappedToOrg { get; set; }
}
