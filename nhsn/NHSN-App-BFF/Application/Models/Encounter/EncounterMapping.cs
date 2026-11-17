namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Encounter;

// GET/PUT /encounter-mappings item shape, and the type of FacilityDraftResponse.Encounter.Mappings.
// Mirrors NHSN-App-UI/src/core/api/contracts.ts EncounterMapping — a field added there needs a
// matching change here.
public sealed record EncounterMapping
{
    public required string System { get; init; }

    public required string Code { get; init; }

    public string? Display { get; init; }

    public required string EncounterType { get; init; }
}
