namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Hsloc;

// GET/PUT /hsloc-mappings item shape. Mirrors NHSN-App-UI/src/core/api/contracts.ts HslocMapping —
// a field added there needs a matching change here. Unlike EncounterMapping, there is no System
// field: every row maps the same facility's single local-location-code space (see
// HslocMappingService.LocalSourceSystem) to the HSLOC vocabulary, never a mix of source systems.
public sealed record HslocMapping
{
    public required string SourceCode { get; init; }

    public string? SourceDisplay { get; init; }

    public required string HslocCode { get; init; }
}
