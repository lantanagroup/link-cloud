namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reference;

public sealed record EncounterCode
{
    public required string System { get; init; }

    public required string Code { get; init; }

    public required string Display { get; init; }

    public string? Category { get; init; }

    public string? CategoryName { get; init; }
}
