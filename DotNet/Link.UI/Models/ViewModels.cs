namespace Link.UI.Models;

public sealed class AdminBffUser
{
    public bool IsAuthenticated { get; init; }
    public string? Email { get; init; }
    public string? UserName { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}

public sealed class TenantListItem
{
    public string FacilityId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool IsDeleted { get; init; }
}

public sealed class TenantListViewModel
{
    public IReadOnlyList<TenantListItem> Tenants { get; init; } = Array.Empty<TenantListItem>();
    public string? Search { get; init; }
    public bool IncludeDeleted { get; init; }
    public string? DeletedNote { get; init; }
    public string? ErrorMessage { get; init; }
    public bool LoadedSuccessfully { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}
