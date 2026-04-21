using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IndexAttribute = Microsoft.EntityFrameworkCore.IndexAttribute;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

/// <summary>
/// Correlation-scoped staging table for reference resource IDs discovered by primary
/// (non-reference) data acquisition logs during their processing.
///
/// Each row represents a single <c>(FacilityId, CorrelationId, ResourceType, ResourceId)</c>
/// tuple that a primary log wants the referential phase to acquire. The unique index on
/// that tuple gives cross-primary deduplication with no write-hot-row contention.
///
/// When all primary-phase logs in a correlation reach a terminal status, a promoter
/// drains these rows into one referential <see cref="DataAcquisitionLog"/> per
/// resource type (with its <see cref="FhirQuery"/> pre-populated from the query-plan
/// <c>ReferenceQueryConfig</c>), deletes the staging rows, and lets the normal
/// acquisition pipeline execute the referential queries — batched by <c>Paged</c> and
/// honoring <c>Search</c> vs <c>SearchPost</c> from the config.
/// </summary>
[Table("PendingReferenceIds")]
[Index(nameof(FacilityId), nameof(CorrelationId), nameof(ResourceType), nameof(ResourceId),
       IsUnique = true, Name = "UX_PendingReferenceIds_Facility_Correlation_Type_Id")]
[Index(nameof(FacilityId), nameof(CorrelationId),
       Name = "IX_PendingReferenceIds_Facility_Correlation")]
public class PendingReferenceId
{
    [Key]
    public long Id { get; set; }

    /// <summary>
    /// Facility that owns this pending reference.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string FacilityId { get; set; } = string.Empty;

    /// <summary>
    /// Correlation id of the primary-phase acquisition that discovered the reference.
    /// Promotion into a referential log is scoped per <c>(FacilityId, CorrelationId)</c>.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// The reference resource type (e.g. "Location", "Medication") used to group ids
    /// into per-type referential queries at promotion time.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// The bare reference resource id (e.g. "Gen-Location-ICU", "med-123"), without
    /// the resource-type prefix.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string ResourceId { get; set; } = string.Empty;

    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
}
