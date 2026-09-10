namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models;

// A page of results, as every list endpoint returns it.
public sealed record Paged<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int TotalCount { get; init; }
}
