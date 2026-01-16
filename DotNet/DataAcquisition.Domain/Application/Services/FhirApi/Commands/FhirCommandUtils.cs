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
    public static TimeSpan? ParseRetryAfter(HttpResponseHeaders? headers)
    {
        if (headers == null || headers.RetryAfter == null) return null;

        var retryValue = headers.RetryAfter;
        TimeSpan? delay = null;

        if (retryValue.Delta.HasValue)
        {
            delay = retryValue.Delta.Value;
        }
        else if (retryValue.Date.HasValue)
        {
            delay = retryValue.Date.Value - DateTimeOffset.UtcNow;
        }

        if (!delay.HasValue || delay.Value <= TimeSpan.Zero)
        {
            delay = DateTime.UtcNow.AddSeconds(60).TimeOfDay;
        }

        return delay;
    }
}