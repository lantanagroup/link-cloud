namespace LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;

/// <summary>
/// A report is absent from the current facility's scope. This deliberately covers both an unknown
/// report id and a report owned by another facility so callers cannot discover foreign reports.
/// </summary>
public sealed class ReportNotFoundException : Exception
{
    public ReportNotFoundException() : base("The requested report was not found.")
    {
    }
}