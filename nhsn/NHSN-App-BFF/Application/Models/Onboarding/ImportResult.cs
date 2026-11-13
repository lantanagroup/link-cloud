namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

// Mirrors ImportResult in NHSN-App-UI/src/core/api/contracts.ts. A cell error names the sheet and
// cell so the message can point the user at exactly where to look, and carries an i18n key rather
// than English text so the frontend renders it in the user's locale.
public sealed record ImportResult
{
    public bool Accepted { get; init; }

    public IReadOnlyList<ImportCellError> CellErrors { get; init; } = [];

    /// How many recognized fields had a non-empty value in the uploaded sheet.
    public int FieldsImported { get; init; }

    /// How many fields the import sheet defines in total.
    public int TotalFields { get; init; }

    /// The sheet's values, one optional slice per step it covers. Null sections had nothing to import.
    public ImportedFields? Fields { get; init; }
}

public sealed record ImportCellError
{
    public required string Sheet { get; init; }

    public required string Cell { get; init; }

    public required string MessageKey { get; init; }

    /// The onboarding StepId (see types.ts) this cell's field belongs to, so the frontend can flag
    /// the right step in the nav. Null for errors that aren't tied to one field (e.g. invalidFormat).
    public string? Section { get; init; }

    /// Interpolated into MessageKey via i18next ({{detail}}) - used only by messageKeys that expect
    /// it (currently just saveFailed), carrying a downstream service's own English explanation
    /// (e.g. "Host must be a valid hostname or IP address.") that can't be pre-registered as a
    /// translation, since its content is decided by that service, not by us.
    public string? Detail { get; init; }

    /// The sheet's own Field Label text for this cell's row (e.g. 'Max Concurrent Requests'),
    /// when the error is tied to a labeled scalar field row. Gives the facility a human-readable
    /// name to go with the bare cell reference, since 'C12' alone means nothing without the sheet
    /// open next to it. Null for sheet-level and section-level (save-failure) errors.
    public string? Label { get; init; }
}
