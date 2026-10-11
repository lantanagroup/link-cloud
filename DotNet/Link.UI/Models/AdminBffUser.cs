namespace Link.UI.Models;

public sealed class AdminBffUser
{
    public bool IsAuthenticated { get; init; }
    public string? Email { get; init; }
    public string? UserName { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}
