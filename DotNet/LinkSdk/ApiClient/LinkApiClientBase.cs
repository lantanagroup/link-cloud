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
    private readonly ILinkCallCredentialSource? _bffCredentials;
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
            var key = tokenServiceSettings.Value?.SigningKey;
            if (!string.IsNullOrWhiteSpace(key))
            {
                _signingKey = key;
                _tokenService = tokenService;
            }
        }
    }

    /// <summary>
    /// Routes this client through Admin.BFF. Relative paths are unchanged; authentication follows
    /// <see cref="AdminBffRoute.Credentials"/> per call instead of always minting the system token.
    /// </summary>
    protected LinkApiClientBase(
        AdminBffRoute route,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken? tokenService)
        : this((route ?? throw new ArgumentNullException(nameof(route))).BaseUrl, bearerOptions, tokenServiceSettings, tokenService)
    {
        _bffCredentials = route.Credentials;
    }

    /// <summary>True when this client sends its calls through Admin.BFF.</summary>
    protected bool RoutedThroughAdminBff => _bffCredentials != null;

    protected IFlurlRequest Request(string relativePath)
    {
        var request = _client.Request(relativePath);

        if (_bffCredentials != null)
        {
            // One hook decides the identity for the whole call, so a forwarded user session and the
            // system token can never travel together.
            var credentials = _bffCredentials;
            request.BeforeCall(async call => await ApplyBffCredentialAsync(call.Request, credentials.Resolve()));
            return request;
        }

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

    private async Task ApplyBffCredentialAsync(IFlurlRequest request, LinkCallCredential credential)
    {
        request.Headers.Remove("Authorization");
        request.Headers.Remove("Cookie");
        request.Headers.Remove(LinkAuditHeaders.InitiatedBy);
        request.Headers.Remove(LinkAuditHeaders.InitiatedByName);

        switch (credential.Kind)
        {
            case LinkCallCredentialKind.ForwardUser:
                if (credential.Cookie != null)
                    request.WithHeader("Cookie", credential.Cookie);
                if (credential.Authorization != null)
                    request.WithHeader("Authorization", credential.Authorization);
                break;

            case LinkCallCredentialKind.SystemOnBehalfOf:
                if (_tokenService != null && _signingKey != null)
                {
                    var token = await _tokenService.ExecuteAsync(_signingKey, 5);
                    if (!string.IsNullOrWhiteSpace(token))
                        request.WithHeader("Authorization", $"Bearer {token}");
                }
                request.WithHeader(LinkAuditHeaders.InitiatedBy, Uri.EscapeDataString(AuditValue(credential.InitiatedById)));
                var name = AuditValue(credential.InitiatedByName);
                if (name.Length > 0)
                    request.WithHeader(LinkAuditHeaders.InitiatedByName, Uri.EscapeDataString(name));
                break;
        }
    }

    internal static string AuditValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var chars = value.Where(c => !char.IsControl(c)).Take(LinkAuditHeaders.MaxValueLength).ToArray();
        return new string(chars).Trim();
    }

    /// <summary>
    /// Executes a request and returns the full response (status code + deserialized body).
    /// Does NOT swallow any status codes — the caller decides how to handle each response.
    /// </summary>
    /// <param name="action">The request to send.</param>
    /// <param name="captureRequestBody">
    /// When false, the request body is not copied into the response's RequestBody. Pass false for any
    /// request whose body carries a credential, since callers display and log RequestBody.
    /// </param>
    protected static async Task<LinkApiResponse<T>> SendAsync<T>(Func<Task<IFlurlResponse>> action, bool captureRequestBody = true)
    {
        try
        {
            var response = await action();
            var statusCode = response.StatusCode;
            var requestUrl = response.ResponseMessage.RequestMessage?.RequestUri?.ToString();
            var requestMethod = response.ResponseMessage.RequestMessage?.Method.Method;
            var requestBody = captureRequestBody ? await ExtractRequestBodyAsync(response.ResponseMessage.RequestMessage) : null;
            var traceId = ExtractTraceId(response);

            if (statusCode is >= 200 and < 300)
            {
                if (statusCode == 204 || response.ResponseMessage.Content?.Headers.ContentLength == 0)
                {
                    return new LinkApiResponse<T> { StatusCode = statusCode, RequestUrl = requestUrl, RequestMethod = requestMethod, RequestBody = requestBody, TraceId = traceId };
                }

                var body = await response.GetJsonAsync<T>();
                string? rawBody = null;
                if (body is not null)
                {
                    try
                    {
                        rawBody = JsonSerializer.Serialize(body);
                    }
                    catch
                    {
                        rawBody = body.ToString();
                    }
                }

                return new LinkApiResponse<T> { StatusCode = statusCode, Body = body, RawBody = rawBody, RequestUrl = requestUrl, RequestMethod = requestMethod, RequestBody = requestBody, TraceId = traceId };
            }
            var raw = await response.GetStringAsync();
            return new LinkApiResponse<T> { StatusCode = statusCode, RawBody = raw, RequestUrl = requestUrl, RequestMethod = requestMethod, RequestBody = requestBody, TraceId = traceId };
        }
        catch (FlurlHttpException ex)
        {
            var raw = await ex.GetResponseStringAsync();
            var requestUrl = ex.Call?.Request?.Url?.ToString();
            var requestMethod = ex.Call?.HttpRequestMessage?.Method.Method;
            var requestBody = captureRequestBody ? await ExtractRequestBodyAsync(ex.Call?.HttpRequestMessage) : null;
            var traceId = ExtractTraceId(ex.Call?.Response);
            return new LinkApiResponse<T> { StatusCode = ex.StatusCode ?? 0, RawBody = raw, RequestUrl = requestUrl, RequestMethod = requestMethod, RequestBody = requestBody, TraceId = traceId };
        }
    }

    /// <summary>
    /// Executes a request and returns the full response (status code + string body).
    /// Does NOT swallow any status codes.
    /// </summary>
    protected static async Task<LinkApiResponse<string>> SendStringAsync(Func<Task<IFlurlResponse>> action)
    {
        try
        {
            var response = await action();
            var statusCode = response.StatusCode;
            var body = await response.GetStringAsync();
            var requestUrl = response.ResponseMessage.RequestMessage?.RequestUri?.ToString();
            var requestMethod = response.ResponseMessage.RequestMessage?.Method.Method;
            var requestBody = await ExtractRequestBodyAsync(response.ResponseMessage.RequestMessage);
            var traceId = ExtractTraceId(response);

            if (statusCode is >= 200 and < 300)
                return new LinkApiResponse<string> { StatusCode = statusCode, Body = body, RawBody = body, RequestUrl = requestUrl, RequestMethod = requestMethod, RequestBody = requestBody, TraceId = traceId };
            return new LinkApiResponse<string> { StatusCode = statusCode, RawBody = body, RequestUrl = requestUrl, RequestMethod = requestMethod, RequestBody = requestBody, TraceId = traceId };
        }
        catch (FlurlHttpException ex)
        {
            var raw = await ex.GetResponseStringAsync();
            var requestUrl = ex.Call?.Request?.Url?.ToString();
            var requestMethod = ex.Call?.HttpRequestMessage?.Method.Method;
            var requestBody = await ExtractRequestBodyAsync(ex.Call?.HttpRequestMessage);
            var traceId = ExtractTraceId(ex.Call?.Response);
            return new LinkApiResponse<string> { StatusCode = ex.StatusCode ?? 0, RawBody = raw, RequestUrl = requestUrl, RequestMethod = requestMethod, RequestBody = requestBody, TraceId = traceId };
        }
    }

    /// <summary>
    /// Executes a request and returns the response status code (no body deserialization).
    /// Does NOT swallow any status codes.
    /// </summary>
    /// <param name="action">The request to send.</param>
    /// <param name="captureRequestBody">
    /// When false, the request body is not copied into the response's RequestBody. Pass false for any
    /// request whose body carries a credential, since callers display and log RequestBody.
    /// </param>
    protected static async Task<LinkApiResponse> SendAsync(Func<Task<IFlurlResponse>> action, bool captureRequestBody = true)
    {
        try
        {
            var response = await action();
            var raw = await response.GetStringAsync();
            var requestUrl = response.ResponseMessage.RequestMessage?.RequestUri?.ToString();
            var requestMethod = response.ResponseMessage.RequestMessage?.Method.Method;
            var requestBody = captureRequestBody ? await ExtractRequestBodyAsync(response.ResponseMessage.RequestMessage) : null;
            var traceId = ExtractTraceId(response);
            return new LinkApiResponse { StatusCode = response.StatusCode, RawBody = raw, RequestUrl = requestUrl, RequestMethod = requestMethod, RequestBody = requestBody, TraceId = traceId };
        }
        catch (FlurlHttpException ex)
        {
            var raw = await ex.GetResponseStringAsync();
            var requestUrl = ex.Call?.Request?.Url?.ToString();
            var requestMethod = ex.Call?.HttpRequestMessage?.Method.Method;
            var requestBody = captureRequestBody ? await ExtractRequestBodyAsync(ex.Call?.HttpRequestMessage) : null;
            var traceId = ExtractTraceId(ex.Call?.Response);
            return new LinkApiResponse { StatusCode = ex.StatusCode ?? 0, RawBody = raw, RequestUrl = requestUrl, RequestMethod = requestMethod, RequestBody = requestBody, TraceId = traceId };
        }
    }

    /// <summary>
    /// Executes a request and returns the response with raw bytes as body.
    /// </summary>
    protected static async Task<LinkApiResponse<byte[]>> SendBytesAsync(Func<Task<IFlurlResponse>> action)
    {
        try
        {
            var response = await action();
            var statusCode = response.StatusCode;
            var contentType = response.ResponseMessage.Content?.Headers?.ContentType?.ToString();
            var requestUrl = response.ResponseMessage.RequestMessage?.RequestUri?.ToString();
            var requestMethod = response.ResponseMessage.RequestMessage?.Method.Method;
            var requestBody = await ExtractRequestBodyAsync(response.ResponseMessage.RequestMessage);
            var traceId = ExtractTraceId(response);

            if (statusCode is >= 200 and < 300)
            {
                var bytes = await response.GetBytesAsync();
                return new LinkApiResponse<byte[]> { StatusCode = statusCode, Body = bytes, ContentType = contentType, RequestUrl = requestUrl, RequestMethod = requestMethod, RequestBody = requestBody, TraceId = traceId };
            }
            var raw = await response.GetStringAsync();
            return new LinkApiResponse<byte[]> { StatusCode = statusCode, RawBody = raw, ContentType = contentType, RequestUrl = requestUrl, RequestMethod = requestMethod, RequestBody = requestBody, TraceId = traceId };
        }
        catch (FlurlHttpException ex)
        {
            var raw = await ex.GetResponseStringAsync();
            var contentType = ex.Call?.Response?.ResponseMessage.Content?.Headers?.ContentType?.ToString();
            var requestUrl = ex.Call?.Request?.Url?.ToString();
            var requestMethod = ex.Call?.HttpRequestMessage?.Method.Method;
            var requestBody = await ExtractRequestBodyAsync(ex.Call?.HttpRequestMessage);
            var traceId = ExtractTraceId(ex.Call?.Response);
            return new LinkApiResponse<byte[]> { StatusCode = ex.StatusCode ?? 0, RawBody = raw, ContentType = contentType, RequestUrl = requestUrl, RequestMethod = requestMethod, RequestBody = requestBody, TraceId = traceId };
        }
    }

    private static string? ExtractTraceId(IFlurlResponse? response)
    {
        if (response == null) return null;
        if (response.Headers.TryGetFirst("traceparent", out var traceparent) && !string.IsNullOrWhiteSpace(traceparent))
        {
            var parts = traceparent.Split('-', StringSplitOptions.TrimEntries);
            if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1]))
                return parts[1];
        }
        if (response.Headers.TryGetFirst("X-Trace-Id", out var xTraceId) && !string.IsNullOrWhiteSpace(xTraceId))
            return xTraceId;
        return null;
    }

    private static async Task<string?> ExtractRequestBodyAsync(HttpRequestMessage? requestMessage)
    {
        if (requestMessage?.Content == null)
            return null;

        try
        {
            var body = await requestMessage.Content.ReadAsStringAsync();
            return string.IsNullOrWhiteSpace(body) ? null : body;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
