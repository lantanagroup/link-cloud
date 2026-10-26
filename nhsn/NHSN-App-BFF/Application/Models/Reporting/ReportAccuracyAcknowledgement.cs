namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

public sealed class ReportAccuracyAcknowledgementRequest
{
    public required bool Accepted { get; set; }
    public required string StatementKey { get; set; }
}

public sealed record ReportAccuracyAcknowledgementResponse
{
    public bool? Accepted { get; init; }
}
