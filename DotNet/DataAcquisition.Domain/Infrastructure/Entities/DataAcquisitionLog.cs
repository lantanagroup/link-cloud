using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("DataAcquisitionLog")]
public class DataAcquisitionLog
{
    public const int MaxRetryAttempts = 5;

    [Required]
    [MaxLength(128)]
    public string FacilityId { get; set; }

    [Required]
    public AcquisitionPriority? Priority { get; set; } = AcquisitionPriority.Normal;

    public string? PatientId { get; set; }

    public string? CorrelationId { get; set; }

    [MaxLength(128)]
    public string? ReferenceResourceType { get; set; }

    public string? FhirVersion { get; set; }

    public FhirQueryType? QueryType { get; set; }

    public QueryPhase? QueryPhase { get; set; }

    [MaxLength(50)]
    public RequestStatus? Status { get; set; }

    public DateTime? ExecutionDate { get; set; }

    public int? RetryAttempts { get; set; }

    public DateTime? CompletionDate { get; set; }

    public long? CompletionTimeMilliseconds { get; set; }

    [ForeignKey("ReportTrackingId")]
    public virtual ScheduledReportEntity? ScheduledReportEntity { get; set; }

    public bool IsCensus { get; set; } = false;

    public ReportableEvent? ReportableEvent { get; set; }

    public bool TailSent { get; set; } = false;

    public bool IsDeleted { get; set; } = false;

    public Guid? ReportTrackingId { get; set; }

    [StringLength(64)]
    public string? TraceId { get; set; }

    /// <summary>
    /// The total number of sibling logs created in the same
    /// (FacilityId, CorrelationId, QueryPhase) group.
    /// Stamped by the creator after all logs are committed.
    /// Null means creation is still in progress (or legacy row).
    /// </summary>
    public int? SiblingCount { get; set; }

    [Key]
    public long Id { get; set; }
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;

    public DateTime? ModifyDate { get; set; }

    [InverseProperty("DataAcquisitionLog")]
    public virtual ICollection<FhirQuery> FhirQueries { get; set; } = new List<FhirQuery>();

    [InverseProperty("DataAcquisitionLog")]
    public virtual ICollection<DataAcquisitionLogNote> NoteEntries { get; set; } = new List<DataAcquisitionLogNote>();

    [InverseProperty("DataAcquisitionLog")]
    public virtual ICollection<DataAcquisitionLogResourceId> ResourceIds { get; set; } = new List<DataAcquisitionLogResourceId>();

    public virtual ICollection<ReferenceResources> ReferenceResources { get; set; } = new List<ReferenceResources>();
}
