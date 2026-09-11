namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reference;

// GET /reference/hsloc-codes item shape. Mirrors NHSN-App-UI/src/core/api/contracts.ts HslocCode —
// a field added there needs a matching change here. FacilityTypes carries the same string tokens
// as contracts.ts HslocFacilityType (e.g. "acuteCareAll", "ltac") rather than a C# enum, since the
// UI treats them as opaque badge keys it looks up localization strings by.
public sealed record HslocCode
{
    public required string Code { get; init; }

    public required string Display { get; init; }

    public string? Category { get; init; }

    public string? Type { get; init; }

    public string? Definition { get; init; }

    public IReadOnlyList<string>? FacilityTypes { get; init; }
}
