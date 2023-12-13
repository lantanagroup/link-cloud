using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.Census.Domain.Entities;

[BsonCollection("censusPatientListEntity")]
public class CensusPatientListEntity : BaseEntity
{
    public string FacilityId { get; set; }
    public string PatientId { get; set; }
    public string DisplayName { get; set; }
    public DateTime AdmitDate { get; set; }
    public bool IsDischarged { get; set; }
    public DateTime DischargeDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}
