namespace Onboarding.Chat.Services;

public sealed class ChatSettings
{
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string McpEndpoint { get; set; } = "http://127.0.0.1:8090/mcp";
    public string Model { get; set; } = "gemma4:e2b";
    public int ResponseTimeoutSeconds { get; set; } = 180;

    public static bool IsAllowedHttpUrl(string value, string? containerHost = null) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.IsLoopback || string.Equals(uri.Host, containerHost, StringComparison.OrdinalIgnoreCase))
        && uri.Scheme == Uri.UriSchemeHttp
        && string.IsNullOrEmpty(uri.UserInfo);
}
