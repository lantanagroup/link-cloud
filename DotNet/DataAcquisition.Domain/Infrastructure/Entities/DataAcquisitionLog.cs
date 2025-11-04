using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("DataAcquisitionLog")]
public class DataAcquisitionLog
{
    [Required]
    public string FacilityId { get; set; }

    [Required]
    public AcquisitionPriority Priority { get; set; } = AcquisitionPriority.Normal;

    public string? PatientId { get; set; }

    public string? CorrelationId { get; set; }

    public string? FhirVersion { get; set; }

    public FhirQueryType? QueryType { get; set; }

    public QueryPhase? QueryPhase { get; set; }

    public RequestStatus? Status { get; set; }

    public DateTime? ExecutionDate { get; set; }

    public int? RetryAttempts { get; set; }

    public DateTime? CompletionDate { get; set; }

    public long? CompletionTimeMilliseconds { get; set; }

    public List<string>? ResourceAcquiredIds { get; set; } = new();

    public List<string> Notes { get; set; } = new();

    public ScheduledReport? ScheduledReport { get; set; }

    public bool IsCensus { get; set; } = false;

    public ReportableEvent? ReportableEvent { get; set; }

    public string? ResourceId { get; set; }

    public bool TailSent { get; set; } = false;

    public string? ReportTrackingId { get; set; }

    public DateTime? ReportEndDate { get; set; }

    public DateTime? ReportStartDate { get; set; }

    [StringLength(64)]
    public string? TraceId { get; set; }

    [Key]
    public long Id { get; set; }
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;

    public DateTime? ModifyDate { get; set; }

    [InverseProperty("DataAcquisitionLog")]
    public virtual ICollection<FhirQuery> FhirQueries { get; set; } = new List<FhirQuery>();

    [InverseProperty("DataAcquisitionLog")]
    public virtual ICollection<ReferenceResources> ReferenceResources { get; set; } = new List<ReferenceResources>();
}
