using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol;

namespace LantanaGroup.Link.Onboarding.Mcp;

public sealed record LuxeraEndpointMatch(string? OrganizationName, string FhirServerBaseUrl, string? VendorName,
    string? City, string? State, string? FhirVersion, string[] Access, string? Status, string? LastUpdated);
public sealed record LuxeraEndpointSearchMetadata(int PageSize, int PageNumber, int TotalCount, int TotalPages);
public sealed record LuxeraEndpointSearchResult(string Status, LuxeraEndpointMatch[] Records, LuxeraEndpointSearchMetadata Metadata,
    string Source, DateTimeOffset? RetrievedAt, string Message, long? RetryAfterSeconds = null);

/// <summary>Searches Luxera's FHIR directory with a process-wide cache and quota coordination.</summary>
public sealed class LuxeraEndpointApi(IHttpClientFactory httpClientFactory, TimeProvider clock) : IDisposable
{
    public const string ClientName = "LuxeraDirectory";
    public const string Source = "https://fhir-api.luxera.io/api/v1/endpoints";
    private const int UpstreamPageSize = 100;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<(string Search, int Page), CachedPage> cache = new();
    private DateTimeOffset nextRequestAt;
    private sealed record CachedPage(ApiEndpoint[] Records, int Total, DateTimeOffset RetrievedAt);
    public sealed record ApiEnvelope(bool Success, ApiEndpoint[]? Data, ApiMeta? Meta);
    public sealed record ApiMeta(int Total, int Page, int Limit);
    public sealed record ApiEndpoint(string? OrganizationName, string? Url, string? VendorName, string? VendorId,
        string? City, string? State, string? FhirVersion, string[]? Access, string? Status, string? LastUpdated);

    public async Task<LuxeraEndpointSearchResult> SearchAsync(string search, int pageNumber, int pageSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(search) || search.Length > 200 || search.Any(char.IsControl))
            throw new McpException("Provide an organization name of at most 200 characters.");
        if (pageNumber < 1 || pageSize is < 1 or > 25 || (long)pageNumber * pageSize > 100000)
            throw new McpException("pageNumber must be positive, pageSize must be between 1 and 25, and the requested page must end within the first 100000 records.");
        search = search.Trim().ToLowerInvariant();
        var metadata = new LuxeraEndpointSearchMetadata(pageSize, pageNumber, 0, 0);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var now = clock.GetUtcNow();
            foreach (var key in cache.Where(pair => now - pair.Value.RetrievedAt >= TimeSpan.FromHours(1)).Select(pair => pair.Key).ToArray())
                cache.Remove(key);
            var offset = (pageNumber - 1) * pageSize;
            var upstreamPage = offset / UpstreamPageSize + 1;
            var skip = offset % UpstreamPageSize;
            var records = new List<LuxeraEndpointMatch>();
            DateTimeOffset? retrievedAt = null;
            var total = 0;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var key = (search, upstreamPage);
                if (!cache.TryGetValue(key, out var page))
                {
                    now = clock.GetUtcNow();
                    if (now < nextRequestAt)
                        return Failure("rate_limited", "Luxera's request quota requires a pause. Retry only after retryAfterSeconds has elapsed.", metadata);
                    using var client = httpClientFactory.CreateClient(ClientName);
                    using var response = await client.GetAsync($"{Source}?query={Uri.EscapeDataString(search)}&page={upstreamPage}&limit={UpstreamPageSize}", cancellationToken);
                    ObserveQuota(response);
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        var retry = response.Headers.RetryAfter;
                        var resumeAt = retry?.Date ?? clock.GetUtcNow().Add(retry?.Delta ?? TimeSpan.FromSeconds(60));
                        if (resumeAt <= clock.GetUtcNow()) resumeAt = clock.GetUtcNow().AddSeconds(1);
                        if (resumeAt > nextRequestAt) nextRequestAt = resumeAt;
                        return Failure("rate_limited", "Luxera has rate limited directory searches. Retry only after retryAfterSeconds has elapsed.", metadata);
                    }
                    if (!response.IsSuccessStatusCode)
                        return Failure("unavailable", "Luxera's endpoint directory is unavailable. No search results were confirmed.", metadata);
                    var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope>(cancellationToken);
                    if (envelope is not { Success: true, Data: not null, Meta: not null }
                        || envelope.Meta.Total < 0 || envelope.Meta.Page != upstreamPage
                        || envelope.Meta.Limit != UpstreamPageSize
                        || envelope.Data.Length != Math.Min(UpstreamPageSize, Math.Max(0, envelope.Meta.Total - (upstreamPage - 1) * UpstreamPageSize))
                        || envelope.Data.Any(endpoint => endpoint is null || !IsSafeUrl(endpoint.Url)))
                        throw new JsonException("Unexpected directory response.");
                    page = new(envelope.Data, envelope.Meta.Total, clock.GetUtcNow());
                    if (cache.Count >= 128)
                        cache.Remove(cache.MinBy(pair => pair.Value.RetrievedAt).Key);
                    cache[key] = page;
                }
                retrievedAt = retrievedAt is null || page.RetrievedAt < retrievedAt ? page.RetrievedAt : retrievedAt;
                total = page.Total;
                foreach (var endpoint in page.Records.Skip(skip).Take(pageSize - records.Count))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    records.Add(new(endpoint.OrganizationName, endpoint.Url!, endpoint.VendorName, endpoint.City, endpoint.State,
                        endpoint.FhirVersion, endpoint.Access ?? [], endpoint.Status, endpoint.LastUpdated));
                }
                skip = 0;
                upstreamPage++;
            } while (records.Count < pageSize && (upstreamPage - 1) * UpstreamPageSize < total);
            return new("available", records.ToArray(), new(pageSize, pageNumber, total, (int)Math.Ceiling((double)total / pageSize)),
                Source, retrievedAt, "Published endpoints from Luxera. Directory inclusion does not establish connectivity or backend-service access.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("unavailable", "The Luxera directory request timed out. No search results were confirmed.", metadata);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
        {
            return Failure("unavailable", "Luxera's endpoint directory could not be read. No search results were confirmed.", metadata);
        }
        finally { gate.Release(); }
    }

    private LuxeraEndpointSearchResult Failure(string status, string message, LuxeraEndpointSearchMetadata metadata) =>
        new(status, [], metadata, Source, null, message,
            status == "rate_limited" ? Math.Max(1, (long)Math.Ceiling((nextRequestAt - clock.GetUtcNow()).TotalSeconds)) : null);

    private void ObserveQuota(HttpResponseMessage response)
    {
        if (!TryHeader(response, "RateLimit-Remaining", out var remaining) || remaining < 0) return;
        var now = clock.GetUtcNow();
        var reset = TryHeader(response, "RateLimit-Reset", out var unix) && unix is >= 0 and <= 253402300799
            ? DateTimeOffset.FromUnixTimeSeconds(unix) : (DateTimeOffset?)null;
        if (remaining == 0)
        {
            var resumeAt = reset > now ? reset.Value : now.AddSeconds(60);
            if (resumeAt > nextRequestAt) nextRequestAt = resumeAt;
        }
        else if (reset > now)
        {
            // Spread remaining requests across the advertised window without assuming a plan's quota.
            var resumeAt = now.AddTicks((long)((reset.Value - now).Ticks / ((decimal)remaining + 1)));
            if (resumeAt > nextRequestAt) nextRequestAt = resumeAt;
        }
    }

    private static bool TryHeader(HttpResponseMessage response, string name, out long value)
    {
        value = 0;
        return response.Headers.TryGetValues(name, out var values)
            && long.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsSafeUrl(string? address) => Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);

    public void Dispose() => gate.Dispose();
}
