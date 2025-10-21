using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IndexAttribute = Microsoft.EntityFrameworkCore.IndexAttribute;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

/// <summary>
/// Durable per-log bag of discovered reference resource IDs.
///
/// A single same-phase reference <see cref="DataAcquisitionLog"/> is created per
/// <c>(FacilityId, CorrelationId, QueryPhase, ResourceType)</c>. As primary logs
/// discover references, their ids are appended here against that one log. The normal
/// pending-log workflow later executes the reference log once all non-reference logs in
/// the phase are terminal.
/// </summary>
[Table("PendingReferenceIds")]
[Index(nameof(DataAcquisitionLogId), nameof(ResourceId),
       IsUnique = true, Name = "UX_PendingReferenceIds_Log_ResourceId")]
[Index(nameof(DataAcquisitionLogId), Name = "IX_PendingReferenceIds_DataAcquisitionLogId")]
public class PendingReferenceId
{
    [Key]
    public long Id { get; set; }

    public long DataAcquisitionLogId { get; set; }

    /// <summary>
    /// Facility that owns this pending reference.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string FacilityId { get; set; } = string.Empty;

    /// <summary>
    /// Correlation id of the report run that owns the reference log.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// The reference resource type (e.g. "Location", "Medication") associated with the
    /// owning reference log.
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

    [ForeignKey(nameof(DataAcquisitionLogId))]
    public DataAcquisitionLog DataAcquisitionLog { get; set; } = null!;
}
