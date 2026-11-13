namespace LantanaGroup.Link.Shared.Application.Models.Audit;

/// <summary>
/// Audit rows in a UTC <c>CreatedOn</c> window whose notes contain "fail".
/// AuditEventType has no error value and the table has no severity column.
/// The notes producers write for a failed operation use that stem
/// ("Failed to upload...", "processing failure").
/// </summary>
public sealed class AuditErrorCount
{
    public int Hours { get; set; }
    public long Errors { get; set; }
    public DateTime WindowStartUtc { get; set; }
    public DateTime WindowEndUtc { get; set; }
}
