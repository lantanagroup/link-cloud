namespace Link.UI.Models;

public sealed class PagerStep
{
    public string Direction { get; init; } = "next";
    public string? Href { get; init; }
    public bool Enabled { get; init; }
    public bool Panel { get; init; }
}
