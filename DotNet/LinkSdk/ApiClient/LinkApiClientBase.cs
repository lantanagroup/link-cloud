using Flurl.Http;
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
    /// Executes a request and returns the full response (status code + deserialized body).
    /// Does NOT swallow any status codes — the caller decides how to handle each response.
    /// </summary>
    protected static async Task<LinkApiResponse<T>> SendAsync<T>(Func<Task<IFlurlResponse>> action)
    {
        try
        {
            var response = await action();
            var statusCode = response.StatusCode;
            var requestUrl = response.ResponseMessage.RequestMessage?.RequestUri?.ToString();
            var requestMethod = response.ResponseMessage.RequestMessage?.Method.Method;
            var requestBody = await ExtractRequestBodyAsync(response.ResponseMessage.RequestMessage);
            var traceId = ExtractTraceId(response);

            if (statusCode is >= 200 and < 300)
            {
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
            var requestBody = await ExtractRequestBodyAsync(ex.Call?.HttpRequestMessage);
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
    protected static async Task<LinkApiResponse> SendAsync(Func<Task<IFlurlResponse>> action)
    {
        try
        {
            var response = await action();
            var raw = await response.GetStringAsync();
            var requestUrl = response.ResponseMessage.RequestMessage?.RequestUri?.ToString();
            var requestMethod = response.ResponseMessage.RequestMessage?.Method.Method;
            var requestBody = await ExtractRequestBodyAsync(response.ResponseMessage.RequestMessage);
            var traceId = ExtractTraceId(response);
            return new LinkApiResponse { StatusCode = response.StatusCode, RawBody = raw, RequestUrl = requestUrl, RequestMethod = requestMethod, RequestBody = requestBody, TraceId = traceId };
        }
        catch (FlurlHttpException ex)
        {
            var raw = await ex.GetResponseStringAsync();
            var requestUrl = ex.Call?.Request?.Url?.ToString();
            var requestMethod = ex.Call?.HttpRequestMessage?.Method.Method;
            var requestBody = await ExtractRequestBodyAsync(ex.Call?.HttpRequestMessage);
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
