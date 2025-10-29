using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.Census.Domain.Entities.POI;

[Index("CorrelationId", Name = "IX_PatientEvents_CorrelationId")]
[Index("CorrelationId", "CreateDate", Name = "IX_PatientEvents_CorrelationId_CreateDate")]
[Index("CreateDate", Name = "IX_PatientEvents_CreateDate")]
[Index("FacilityId", Name = "IX_PatientEvents_FacilityId")]
[Index("SourcePatientId", Name = "IX_PatientEvents_SourcePatientId")]
public partial class PatientEvent
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string FacilityId { get; set; }

    public string CorrelationId { get; set; }

    [Required]
    public string SourcePatientId { get; set; }

    public string SourceVisitId { get; set; }

    public string MedicalRecordNumber { get; set; }

    [Required]
    [StringLength(255)]
    public EventType EventType { get; set; }

    [Required]
    public IPayload Payload { get; set; }

    [Required]
    [StringLength(255)]
    public SourceType SourceType { get; set; }

    public DateTime CreateDate { get; set; }

    public DateTime? ModifyDate { get; set; }
}
