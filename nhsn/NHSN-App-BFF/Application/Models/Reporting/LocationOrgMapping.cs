namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

public sealed record LocationOrgMapping
{
    public required string LocationId { get; init; }

    public string? LocationName { get; init; }

    public string? LocationAlias { get; init; }

    public string? PartOfValue { get; init; }

    public int? PartOfId { get; init; }

    public bool IsOrgLocation { get; init; }

    public bool IsActive { get; init; }
}
