using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Models;

[ExcludeFromCodeCoverage]
public class EncounterMappingSearchParameters
{
    public string? PatientId { get; set; }
    public string? EncounterId { get; set; }
    public bool? MappedToOrg { get; set; }
}
