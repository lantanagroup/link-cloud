namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reference;

// CodeSystem/$lookup's answer for one code: the source CodeSystem's own name and version.
// Neither is present on the ValueSet/$expand rows EncounterCode is built from, so this is the
// "code-detail lookup" half of the Encounter Mapping reference tab, fetched only for the
// selected row rather than for the whole list.
public sealed record EncounterCodeDetail
{
    public required string System { get; init; }

    public required string Code { get; init; }

    public required string Display { get; init; }

    public string? Name { get; init; }

    public string? Version { get; init; }
}
