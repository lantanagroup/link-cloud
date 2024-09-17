using System.ComponentModel.DataAnnotations.Schema;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.Census.Domain.Entities;

[Table("CensusPatientList")]
public class CensusPatientListEntity : BaseEntityExtended
{
    public string FacilityId { get; set; }
    public string PatientId { get; set; }
    public string? DisplayName { get; set; }
    public DateTime? AdmitDate { get; set; }
    public bool IsDischarged { get; set; }
    public DateTime? DischargeDate { get; set; }
}
