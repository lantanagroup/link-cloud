#nullable disable
using LantanaGroup.Link.Shared.Application.Models.Retention;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Report.Data.Entities;

/// <summary>
/// One report in a retention cleanup, with the window and cutoff that selected it.
/// </summary>
/// <remarks>
/// <see cref="ReportScheduleId"/> is deliberately not a foreign key, so the cleanup history outlives a
/// purged report. See dev-docs/retention-cleanup-record.md.
/// </remarks>
[Index("RetentionCleanupId", "ReportScheduleId", Name = "IX_RetentionCleanupReport_Cleanup_Report", IsUnique = true)]
[Index("ReportScheduleId", Name = "IX_RetentionCleanupReport_ReportScheduleId")]
public partial class RetentionCleanupReport
{
    [Key]
    public Guid Id { get; set; }

    public Guid RetentionCleanupId { get; set; }

    /// <summary>
    /// The report's <c>ReportSchedule.Id</c>.
    /// </summary>
    public Guid ReportScheduleId { get; set; }

    [Required]
    [StringLength(100)]
    public string FacilityId { get; set; }

    /// <summary>
    /// The unit of the window that selected the report. Null on a single-report cleanup, which no window
    /// selected.
    /// </summary>
    public RetentionWindowUnit? RetentionWindowUnit { get; set; }

    /// <summary>
    /// The length of the window that selected the report. Null when <see cref="RetentionWindowUnit"/> is.
    /// </summary>
    public int? RetentionWindowCount { get; set; }

    /// <summary>
    /// The cutoff that selected the report, in UTC. Computed once; a retry never recomputes it.
    /// </summary>
    public DateTimeOffset CutoffDateTime { get; set; }

    [ForeignKey("RetentionCleanupId")]
    public virtual RetentionCleanup RetentionCleanup { get; set; }

    [InverseProperty("RetentionCleanupReport")]
    public virtual ICollection<RetentionCleanupResult> Results { get; set; } = new List<RetentionCleanupResult>();
}
