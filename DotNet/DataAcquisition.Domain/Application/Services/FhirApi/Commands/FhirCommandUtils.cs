using System.Net.Http.Headers;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;

internal class HeaderCapturingHandler : DelegatingHandler
{
    public HttpResponseHeaders? LastResponseHeaders { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        LastResponseHeaders = response.Headers;
        return response;
    }
}

internal static class FhirCommandUtils
{
    public static TimeSpan ParseRetryAfter(HttpResponseHeaders? headers, TimeSpan defaultDelay = default)
    {
        defaultDelay = defaultDelay == default ? TimeSpan.FromSeconds(60) : defaultDelay;

        if (headers == null || headers.RetryAfter == null)
        {
            return defaultDelay;
        }

        var retryValue = headers.RetryAfter;
        TimeSpan delay;

        if (retryValue.Delta.HasValue)
        {
            delay = retryValue.Delta.Value;
        }
        else if (retryValue.Date.HasValue)
        {
            delay = retryValue.Date.Value - DateTimeOffset.UtcNow;
        }
        else
        {
            return defaultDelay;
        }

        if (delay <= TimeSpan.Zero)
        {
            return defaultDelay;
        }

        return delay;
    }
}