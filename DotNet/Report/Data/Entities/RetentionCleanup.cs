#nullable disable
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Shared.Application.Models.Retention;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Report.Data.Entities;

/// <summary>
/// One retention cleanup: the reports it covers, the participants expected to reply, and their results.
/// See dev-docs/retention-cleanup-record.md.
/// </summary>
[Index("RunId", Name = "IX_RetentionCleanup_RunId")]
[Index("Status", Name = "IX_RetentionCleanup_Status")]
[Index("InitiatedDateTime", Name = "IX_RetentionCleanup_InitiatedDateTime", AllDescending = true)]
public partial class RetentionCleanup
{
    /// <summary>
    /// The <c>RetentionCleanupId</c>, kept for the life of the cleanup, retries and re-sends included.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// The run that created the cleanup, shared by every cleanup one large run splits into.
    /// </summary>
    public Guid RunId { get; set; }

    /// <summary>
    /// What started the cleanup.
    /// </summary>
    public RetentionInitiatedBy InitiatedBy { get; set; }

    /// <summary>
    /// When Report created the cleanup, in UTC.
    /// </summary>
    public DateTimeOffset InitiatedDateTime { get; set; }

    /// <summary>
    /// The user id of whoever started the cleanup: <c>SystemUser</c> for a scheduled run, otherwise the
    /// token's <c>sub</c> claim. Never sent on Kafka.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string InitiatedByUserId { get; set; }

    /// <summary>
    /// The display name of whoever started the cleanup, as it was at that moment. Never sent on Kafka.
    /// </summary>
    [Required]
    [StringLength(256)]
    public string InitiatedByUserName { get; set; }

    /// <summary>
    /// How much of each report's data goes.
    /// </summary>
    public RetentionMode Mode { get; set; }

    /// <summary>
    /// The cutoff for the system default window, in UTC, used by <see cref="RetentionParticipantScope.Cutoff"/>
    /// participants. Computed once; a retry never recomputes it.
    /// </summary>
    public DateTimeOffset CutoffDateTime { get; set; }

    /// <summary>
    /// The overall status, recalculated from <see cref="Results"/> on every reply.
    /// </summary>
    public RetentionCleanupStatus Status { get; set; }

    /// <summary>
    /// Why the cleanup failed without sending a request, such as an invalid participant list.
    /// Null when a request was sent.
    /// </summary>
    [StringLength(2000)]
    public string FailureReason { get; set; }

    /// <summary>
    /// When <see cref="Status"/> became <see cref="RetentionCleanupStatus.Completed"/>, in UTC.
    /// Null until then.
    /// </summary>
    public DateTimeOffset? CompletedDateTime { get; set; }

    [InverseProperty("RetentionCleanup")]
    public virtual ICollection<RetentionCleanupReport> Reports { get; set; } = new List<RetentionCleanupReport>();

    [InverseProperty("RetentionCleanup")]
    public virtual ICollection<RetentionCleanupParticipant> Participants { get; set; } =
        new List<RetentionCleanupParticipant>();
}
