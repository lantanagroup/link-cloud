using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

[ExcludeFromCodeCoverage]
public class EncounterMappingModel
{
    public int EncounterMappingId { get; set; }
    public string FacilityId { get; set; }
    public string PatientId { get; set; }
    public string EncounterId { get; set; }
    public bool MappedToOrg { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public List<EncounterLocationModel> EncounterLocations { get; set; } = new List<EncounterLocationModel>();
}
