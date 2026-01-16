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
        if (headers == null) return null;

        if (headers.TryGetValues("Retry-After", out var values))
        {
            var value = values.FirstOrDefault();
            if (int.TryParse(value, out int seconds))
                return TimeSpan.FromSeconds(seconds);
            if (DateTimeOffset.TryParse(value, out var date))
                return date - DateTimeOffset.UtcNow;
        }
        return null;
    }
}