using Flurl.Http;
using Flurl.Http.Configuration;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Sdk.ApiClient;

public abstract class LinkApiClientBase : IDisposable
{
    private readonly IFlurlClient _client;
    private readonly ICreateSystemToken? _tokenService;
    private readonly string? _signingKey;
    private bool _disposed;

    protected LinkApiClientBase(
        string baseUrl,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken? tokenService)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL cannot be null or empty.", nameof(baseUrl));

        _client = new FlurlClient(baseUrl);
        _client.Settings.JsonSerializer = new DefaultJsonSerializer(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        });

        if (bearerOptions.Value?.AllowAnonymous != true)
        {
            _signingKey = tokenServiceSettings.Value?.SigningKey;
            _tokenService = tokenService;
        }
    }

    protected IFlurlRequest Request(string relativePath)
    {
        var request = _client.Request(relativePath);

        if (_tokenService != null && _signingKey != null)
        {
            request.BeforeCall(async call =>
            {
                var token = await _tokenService.ExecuteAsync(_signingKey, 5);
                if (!string.IsNullOrWhiteSpace(token))
                    call.Request.WithHeader("Authorization", $"Bearer {token}");
            });
        }

        return request;
    }

    /// <summary>
    /// Returns null when the server responds with 404. All other errors propagate.
    /// </summary>
    protected static async Task<T?> GetOrDefaultAsync<T>(Func<Task<T>> action) where T : class
    {
        try { return await action(); }
        catch (FlurlHttpException ex) when (ex.StatusCode == 404) { return null; }
    }

    /// <summary>
    /// Returns false when the server responds with 404, true otherwise.
    /// </summary>
    protected static async Task<bool> ExistsAsync(Func<Task> action)
    {
        try { await action(); return true; }
        catch (FlurlHttpException ex) when (ex.StatusCode == 404) { return false; }
    }

    /// <summary>
    /// Returns true if created, false if the resource already exists (409 or 400+"already exists").
    /// </summary>
    protected static async Task<bool> CreateOrExistsAsync(Func<Task> action)
    {
        try { await action(); return true; }
        catch (FlurlHttpException ex) when (ex.StatusCode == 409) { return false; }
        catch (FlurlHttpException ex) when (ex.StatusCode == 400)
        {
            var body = await ex.GetResponseStringAsync();
            if (body?.Contains("already exists", StringComparison.OrdinalIgnoreCase) == true)
                return false;
            throw;
        }
    }

    /// <summary>
    /// Silently succeeds if the resource is already gone (404).
    /// </summary>
    protected static async Task DeleteOrIgnoreAsync(Func<Task> action)
    {
        try { await action(); }
        catch (FlurlHttpException ex) when (ex.StatusCode == 404) { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
