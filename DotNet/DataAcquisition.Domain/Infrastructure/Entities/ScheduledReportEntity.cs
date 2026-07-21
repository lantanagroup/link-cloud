using LantanaGroup.Link.Shared.Application.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

/// <summary>
/// Normalised representation of a scheduled report. One row per unique
/// (ReportTrackingId) — shared by all <see cref="DataAcquisitionLog"/> rows
/// that belong to the same report run.
/// </summary>
[Table("ScheduledReports")]
public class ScheduledReportEntity
{
    [Key]
    public Guid ReportTrackingId { get; set; }

    [Required]
    public Frequency? Frequency { get; set; } = 0;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    /// <summary>
    /// Comma-delimited list of report type identifiers (e.g. measure IDs).
    /// Stored as a simple string to keep the table flat. Typical cardinality is 1.
    /// </summary>
    [MaxLength(2000)]
    public string? ReportTypes { get; set; }

    public DateTime CreateDate { get; set; } = DateTime.UtcNow;

    [InverseProperty("ScheduledReportEntity")]
    public virtual ICollection<DataAcquisitionLog> DataAcquisitionLogs { get; set; } = new List<DataAcquisitionLog>();
}
