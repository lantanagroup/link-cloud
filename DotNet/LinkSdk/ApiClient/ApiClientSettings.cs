namespace LantanaGroup.Link.Sdk.ApiClient;

public sealed class ApiClientSettings
{
    public required string BaseUrl { get; init; }
    public string? BearerToken { get; init; }
}
