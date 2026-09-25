namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

public sealed record PreQualCategory
{
    public required string Title { get; init; }
    public required bool Acceptable { get; init; }
    public required string Guidance { get; init; }
}

public sealed record PreQualIssue
{
    public required PreQualCategory Category { get; init; }
    public required string Message { get; init; }
    public required string Expression { get; init; }
    public required string Location { get; init; }
}
