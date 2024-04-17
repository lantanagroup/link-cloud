using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Census.Domain.Entities;

[Table("CensusPatientList")]
public class CensusPatientListEntity : BaseEntity
{
    public string FacilityId { get; set; }
    public string PatientId { get; set; }
    public string? DisplayName { get; set; }
    public DateTime? AdmitDate { get; set; }
    public bool IsDischarged { get; set; }
    public DateTime? DischargeDate { get; set; }
}
