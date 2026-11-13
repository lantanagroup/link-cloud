namespace Automation.UI.Services;

/// <summary>
/// Picks one page of stored run-log lines. Page 0 means the last page, which
/// is the live tail. Ordering matches the snapshot store: sequence, then write order.
/// </summary>
public static class RunLogPaging
{
    public const int DefaultPageSize = 200;

    public const int MaxPageSize = 500;

    public readonly record struct SourceLine(long Sequence, int Ordinal, string SourceId, int LineIndex);

    public readonly record struct Selection(int PageNumber, int TotalPages, IReadOnlyList<SourceLine> Items);

    public static int NormalizePageSize(int pageSize) =>
        pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

    public static Selection Select(IReadOnlyList<SourceLine> lines, int pageNumber, int pageSize)
    {
        var size = NormalizePageSize(pageSize);
        var ordered = lines
            .OrderBy(line => line.Sequence)
            .ThenBy(line => line.Ordinal)
            .ToList();
        var total = ordered.Count;
        var pages = total == 0 ? 1 : (int)Math.Min(int.MaxValue, ((long)total + size - 1) / size);
        var page = pageNumber <= 0 ? pages : Math.Clamp(pageNumber, 1, pages);
        var skip = (page - 1) * size;
        var count = Math.Max(0, Math.Min(size, total - skip));
        return new Selection(page, pages, ordered.GetRange(skip, count));
    }
}
