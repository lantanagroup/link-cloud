namespace Link.UI.Models;

public sealed class BottomPager
{
    public string Label { get; init; } = "Pages";
    public int Page { get; init; } = 1;
    public long Pages { get; init; } = 1;
    public long Total { get; init; }
    public int PageSize { get; init; }
    public IReadOnlyList<int> Sizes { get; init; } = [];
    public Func<int, int, string>? Href { get; init; }
    public Func<int, int, Dictionary<string, string>>? Route { get; init; }
    public string? Path { get; init; }
    public string PageKey { get; init; } = "page";
    public bool Panel { get; init; }
}
