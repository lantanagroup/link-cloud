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
}

public sealed record ImportCellError
{
    public required string Sheet { get; init; }

    public required string Cell { get; init; }

    public required string MessageKey { get; init; }
}
