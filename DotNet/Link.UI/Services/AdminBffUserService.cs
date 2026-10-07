using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Link.UI.Models;
using Microsoft.AspNetCore.Http;

namespace Link.UI.Services;

public interface IAdminBffUserService
{
    Task<AdminBffUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads the Admin.BFF cookie session via GET /api/user, forwarding the browser cookie.
/// Prefer same-origin YARP for browser login/logout; this client is for MVC server-side checks.
/// </summary>
public sealed class AdminBffUserService : IAdminBffUserService
{
    public const string HttpContextItemKey = "Link.UI.AdminBffUser";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AdminBffUserService> _logger;

    public AdminBffUserService(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AdminBffUserService> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<AdminBffUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items[HttpContextItemKey] is AdminBffUser cached)
            return cached;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/user");
            var cookie = httpContext?.Request.Headers.Cookie.ToString();
            if (!string.IsNullOrWhiteSpace(cookie))
                request.Headers.TryAddWithoutValidation("Cookie", cookie);

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return Cache(httpContext, new AdminBffUser { IsAuthenticated = false });
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Admin.BFF /api/user returned {StatusCode}", (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var dto = await JsonSerializer.DeserializeAsync<AdminBffUserDto>(stream, JsonOptions, cancellationToken);
            if (dto is null || !HasIdentity(dto))
                return Cache(httpContext, new AdminBffUser { IsAuthenticated = false });

            var display = string.Join(' ', new[] { dto.FirstName, dto.LastName }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            var user = new AdminBffUser
            {
                IsAuthenticated = true,
                Email = dto.Email,
                UserName = string.IsNullOrWhiteSpace(display) ? dto.Email : display,
                Roles = dto.Roles ?? Array.Empty<string>()
            };

            return Cache(httpContext, user);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;

            _logger.LogDebug(ex, "Unable to resolve Admin.BFF user session");
            return null;
        }
    }

    private static AdminBffUser Cache(HttpContext? httpContext, AdminBffUser user)
    {
        if (httpContext is not null)
            httpContext.Items[HttpContextItemKey] = user;
        return user;
    }

    /// <summary>
    /// Development Admin.BFF with anonymous access returns 200 and an empty user.
    /// That payload is not a signed-in principal.
    /// </summary>
    private static bool HasIdentity(AdminBffUserDto dto) =>
        !string.IsNullOrWhiteSpace(dto.Email)
        || !string.IsNullOrWhiteSpace(dto.FirstName)
        || !string.IsNullOrWhiteSpace(dto.LastName)
        || dto.Roles is { Length: > 0 }
        || dto.Permissions is { Length: > 0 };

    private sealed class AdminBffUserDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        [JsonPropertyName("roles")]
        public string[]? Roles { get; set; }

        [JsonPropertyName("permissions")]
        public string[]? Permissions { get; set; }
    }
}
