namespace Link.UI.Models;

/// <summary>
/// A displayed identifier: a label, an optional name, the value, and a copy button.
/// Compact keeps the label for assistive tech when a column header already names the cell.
/// </summary>
public sealed class LabeledId
{
    public string Label { get; init; } = "";
    public string? Name { get; init; }
    public string? Value { get; init; }
    public string? CopyLabel { get; init; }
    public string? LinkHref { get; init; }
    public string? ValueElementId { get; init; }
    public bool Compact { get; init; }
    public bool Copy { get; init; } = true;
}

public sealed class CopyTarget
{
    public string Value { get; init; } = "";
    public string Label { get; init; } = "Copy";
}
