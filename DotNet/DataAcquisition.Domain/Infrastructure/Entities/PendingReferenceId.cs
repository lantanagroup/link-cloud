using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

/// <summary>
/// Staging table for reference resource IDs discovered during concurrent patient processing.
/// Each row represents a single reference ID found by one patient worker.
/// This eliminates the write-hot-row contention on the shared FhirQuery.QueryParameters column
/// that previously caused deadlocks and timeouts under high concurrency.
///
/// When the reference log is ready to execute, all pending IDs for its FhirQuery are
/// collected in a single read and merged into the FhirQuery.QueryParameters at that point.
/// </summary>
[Table("PendingReferenceIds")]
public class PendingReferenceId
{
    [Key]
    public long Id { get; set; }

    /// <summary>
    /// The FhirQuery (reference log's query) that these IDs will be merged into.
    /// </summary>
    [Required]
    public Guid FhirQueryId { get; set; }

    [ForeignKey(nameof(FhirQueryId))]
    public virtual FhirQuery FhirQuery { get; set; } = null!;

    /// <summary>
    /// The reference resource ID (e.g. "Location/Gen-Location-ICU", "Medication/med-123").
    /// Stored as the bare ID without the resource type prefix.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// The resource type (e.g. "Location", "Medication") for efficient grouping.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string ResourceType { get; set; } = string.Empty;

    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
}
