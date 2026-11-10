#nullable disable
using LantanaGroup.Link.Report.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Report.Data.Entities;

/// <summary>
/// One participant's result in a retention cleanup: one per report for a report-scoped participant, or one
/// for the whole cleanup, with no report, for a cutoff-scoped one.
/// </summary>
/// <remarks>
/// The unique index on participant and report is configured in <see cref="ReportDbContext"/>. See
/// dev-docs/retention-cleanup-record.md.
/// </remarks>
[Index("RetentionCleanupReportId", Name = "IX_RetentionCleanupResult_RetentionCleanupReportId")]
public partial class RetentionCleanupResult
{
    [Key]
    public Guid Id { get; set; }

    public Guid RetentionCleanupParticipantId { get; set; }

    /// <summary>
    /// The report this result is for. Null for a cutoff-scoped participant's single result.
    /// </summary>
    public Guid? RetentionCleanupReportId { get; set; }

    public RetentionCleanupResultStatus Status { get; set; }

    /// <summary>
    /// What the participant deleted on its latest reply, including before a failure. Null while pending.
    /// </summary>
    public long? RecordsAffected { get; set; }

    /// <summary>
    /// The participant's sanitized description of a failure. Never a stack trace or PHI.
    /// </summary>
    [StringLength(2000)]
    public string ErrorMessage { get; set; }

    /// <summary>
    /// When the participant finished this result, in UTC, whatever the outcome. Null while pending.
    /// </summary>
    public DateTimeOffset? CompletedDateTime { get; set; }

    /// <summary>
    /// When the request was last sent to this participant for this result, in UTC. Drives re-sending.
    /// </summary>
    public DateTimeOffset? LastSentDateTime { get; set; }

    /// <summary>
    /// How many times the request has been sent for this result.
    /// </summary>
    public int SendCount { get; set; }

    [ForeignKey("RetentionCleanupParticipantId")]
    public virtual RetentionCleanupParticipant RetentionCleanupParticipant { get; set; }

    [ForeignKey("RetentionCleanupReportId")]
    public virtual RetentionCleanupReport RetentionCleanupReport { get; set; }
}
