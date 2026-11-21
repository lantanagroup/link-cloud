namespace LantanaGroup.Link.Sdk.Clients;

/// <summary>
/// Body of <c>GET /api/data-acquisition/antiforgery-token</c>.
/// </summary>
public sealed class AntiforgeryTokenApiModel
{
    public string? Token { get; set; }

    public string? HeaderName { get; set; }
}
