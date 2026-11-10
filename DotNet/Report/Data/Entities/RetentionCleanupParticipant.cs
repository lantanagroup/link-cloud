#nullable disable
using LantanaGroup.Link.Report.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Report.Data.Entities;

/// <summary>
/// A participant expected to reply to a retention cleanup, copied from <c>Retention:Participants</c> when the
/// cleanup is created. Later configuration changes do not touch it.
/// </summary>
[Index("RetentionCleanupId", "ServiceName", Name = "IX_RetentionCleanupParticipant_Cleanup_Service",
    IsUnique = true)]
public partial class RetentionCleanupParticipant
{
    [Key]
    public Guid Id { get; set; }

    public Guid RetentionCleanupId { get; set; }

    /// <summary>
    /// The name the participant puts in a completion's <c>ServiceName</c>.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string ServiceName { get; set; }

    public RetentionParticipantScope Scope { get; set; }

    [ForeignKey("RetentionCleanupId")]
    public virtual RetentionCleanup RetentionCleanup { get; set; }

    [InverseProperty("RetentionCleanupParticipant")]
    public virtual ICollection<RetentionCleanupResult> Results { get; set; } = new List<RetentionCleanupResult>();
}
