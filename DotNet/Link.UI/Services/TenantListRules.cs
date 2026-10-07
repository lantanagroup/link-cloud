namespace Link.UI.Services;

/// <summary>
/// Pages the tenant dictionary the facility service returns whole.
/// The list call has no page argument, so the slice happens after that response.
/// </summary>
public static class TenantListRules
{
    public const int DefaultPageSize = 25;
    public static readonly int[] PageSizes = [25, 50, 100];

    public static int ClampPageSize(int pageSize) =>
        PageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

    public static TenantPage<T> Slice<T>(IReadOnlyList<T> items, int page, int pageSize)
    {
        var size = ClampPageSize(pageSize);
        var total = items.Count;
        var pages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)size);
        var number = pages == 0 ? 1 : Math.Clamp(page < 1 ? 1 : page, 1, pages);
        var start = (number - 1) * size;
        var slice = items.Skip(start).Take(size).ToList();
        return new TenantPage<T>(slice, number, size, total, pages);
    }
}

public sealed record TenantPage<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);
