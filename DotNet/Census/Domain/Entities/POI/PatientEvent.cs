using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace LantanaGroup.Link.Census.Domain.Entities.POI;

[Table("PatientEvents")]
public class PatientEvent : BaseEntityExtended
{
    [Required]
    public string FacilityId { get; set; }
    public string? CorrelationId { get; set; }
    [Required]
    public string SourcePatientId { get; set; }
    public string? SourceVisitId { get; set; }
    public string? MedicalRecordNumber { get; set; }
    [Column(TypeName = "nvarchar(255)")]
    public EventType EventType { get; set; }
    public string Payload { get; set; }
    [Column(TypeName = "nvarchar(255)")]
    public SourceType SourceType { get; set; }

    public IPayload GetPayload()
    {
        IPayload payload = null;

        switch (this.SourceType)
        {
            case SourceType.FHIR:
                if (this.EventType == EventType.FHIRListAdmit)
                    return JsonSerializer.Deserialize<FHIRListAdmitPayload>(this.Payload);
                else if (this.EventType == EventType.FHIRListDischarge)
                    return JsonSerializer.Deserialize<FHIRListDischargePayload>(this.Payload);
                break;
                //else
                //    return JsonSerializer.Deserialize<FHIREncounterAcquiredPayload>(this.Payload);
            case SourceType.ADT:
                //if (this.EventType == EventType.A01)
                //    return JsonSerializer.Deserialize<A01Payload>(this.Payload);
                //else
                //    return JsonSerializer.Deserialize<A03Payload>(this.Payload);
                break;
            case SourceType.SFTP:
                //TODO - Daniel: Need to add
                break;
        }

        return payload;
    }
}
