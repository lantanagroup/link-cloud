using System.Text.Json;
using ModelContextProtocol;

namespace LantanaGroup.Link.Onboarding.Mcp;

public sealed record EpicEndpointMatch(string FacilityName, string BrandName, string? City, string? State,
    string FhirServerBaseUrl);
public sealed record EpicEndpointMetadata(int PageSize, int PageNumber, int TotalCount, int TotalPages);
public sealed record EpicEndpointLookupResult(string Status, string Source, DateTimeOffset? RetrievedAt,
    EpicEndpointMatch[] Records, EpicEndpointMetadata Metadata, string Message);

// Keep endpoint discovery independent of facility settings and credentials.
public sealed class EpicEndpointDirectory(HttpClient httpClient, ILogger<EpicEndpointDirectory> logger) : BackgroundService
{
    public const string Source = "https://open.epic.com/MyApps/Endpoints";
    public const string DownloadUrl = "https://open.epic.com/Endpoints/Brands";
    private sealed record Snapshot(EpicEndpointMatch[] Records, DateTimeOffset RetrievedAt);
    private Snapshot? snapshot;
    private string downloadStatus = "The Epic directory is still downloading. The initial download may take up to five minutes; try again shortly.";

    public EpicEndpointLookupResult Lookup(string search, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        search = search?.Trim() ?? "";
        if (search.Length is < 2 or > 200 || search.Any(char.IsControl))
            throw new McpException("search must contain 2 through 200 characters without control characters.");
        if (pageNumber < 1 || pageSize is < 1 or > 50)
            throw new McpException("pageNumber must be positive and pageSize must be 1 through 50.");
        var current = Volatile.Read(ref snapshot);
        if (current is null)
            return new("unavailable", Source, null, [], new(pageSize, pageNumber, 0, 0),
                Volatile.Read(ref downloadStatus));
        var words = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matches = new List<EpicEndpointMatch>();
        foreach (var record in current.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = $"{record.FacilityName} {record.BrandName} {record.City} {record.State}";
            if (words.All(word => text.Contains(word, StringComparison.OrdinalIgnoreCase))) matches.Add(record);
        }
        var stale = DateTimeOffset.UtcNow - current.RetrievedAt > TimeSpan.FromDays(7);
        return new(stale ? "stale" : "available", Source, current.RetrievedAt,
            matches.Skip((int)Math.Min((long)(pageNumber - 1) * pageSize, matches.Count)).Take(pageSize).ToArray(),
            new(pageSize, pageNumber, matches.Count, (int)Math.Ceiling((double)matches.Count / pageSize)),
            "Published Epic R4 patient-access endpoint candidates. Confirm the facility and intended integration before saving; directory inclusion does not confirm connectivity or backend-service access.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var success = await RefreshAsync(stoppingToken);
            await Task.Delay(success ? TimeSpan.FromDays(7) : TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            // A fixed public URL; no search terms, tokens, or facility identifiers leave the service.
            using var response = await httpClient.GetAsync(DownloadUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            var records = Parse(document.RootElement, cancellationToken);
            if (records.Length == 0) throw new JsonException();
            Volatile.Write(ref snapshot, new(records, DateTimeOffset.UtcNow));
            logger.LogInformation("Epic endpoint directory refreshed with {EndpointCount} facility endpoint records.", records.Length);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Volatile.Write(ref downloadStatus, "The Epic directory download timed out. An automatic retry runs in one minute; check the MCP logs and outbound access to open.epic.com if this persists.");
            logger.LogWarning("Epic endpoint directory download timed out. Retrying in one minute.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            Volatile.Write(ref downloadStatus, "The Epic directory download failed. An automatic retry runs in one minute; check the MCP logs and outbound access to open.epic.com.");
            logger.LogWarning("Epic endpoint directory refresh failed ({FailureType}, HTTP {HttpStatus}); retaining any previous snapshot and retrying in one minute.",
                ex.GetType().Name, (ex as HttpRequestException)?.StatusCode);
        }
        return false;
    }

    private static EpicEndpointMatch[] Parse(JsonElement bundle, CancellationToken cancellationToken)
    {
        if (Text(bundle, "resourceType") != "Bundle") throw new JsonException();
        var resources = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var organizations = new List<JsonElement>();
        foreach (var entry in bundle.GetProperty("entry").EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resource = entry.GetProperty("resource");
            var type = Text(resource, "resourceType");
            var id = Text(resource, "id");
            if (id is not null) resources[$"{type}/{id}"] = resource;
            if (Text(entry, "fullUrl") is { } fullUrl) resources[fullUrl] = resource;
            if (type == "Organization") organizations.Add(resource);
        }
        var result = new HashSet<EpicEndpointMatch>();
        foreach (var organization in organizations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (organization.TryGetProperty("active", out var active) && active.ValueKind == JsonValueKind.False) continue;
            var name = Text(organization, "name");
            if (string.IsNullOrWhiteSpace(name)) continue;
            var brand = organization;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (!brand.TryGetProperty("endpoint", out _) && brand.TryGetProperty("partOf", out var parent)
                && Text(parent, "reference") is { } reference && visited.Add(reference)
                && resources.TryGetValue(reference, out var resolved)) brand = resolved;
            if (Text(brand, "resourceType") != "Organization" || !brand.TryGetProperty("endpoint", out var endpoints)) continue;
            var addresses = organization.TryGetProperty("address", out var addressArray)
                ? addressArray.EnumerateArray().ToArray() : [];
            foreach (var endpointReference in endpoints.EnumerateArray())
            {
                if (Text(endpointReference, "reference") is not { } endpointRef || !resources.TryGetValue(endpointRef, out var endpoint)
                    || Text(endpoint, "resourceType") != "Endpoint" || Text(endpoint, "status") != "active"
                    || !endpoint.TryGetProperty("connectionType", out var connectionType)
                    || Text(connectionType, "code") != "hl7-fhir-rest") continue;
                var url = Text(endpoint, "address");
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https"
                    || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query)
                    || !string.IsNullOrEmpty(uri.Fragment)) continue;
                if (addresses.Length == 0) result.Add(new(name, Text(brand, "name") ?? name, null, null, url!));
                foreach (var address in addresses)
                    result.Add(new(name, Text(brand, "name") ?? name, Text(address, "city"), Text(address, "state"), url!));
            }
        }
        return result.OrderBy(x => x.FacilityName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.State).ThenBy(x => x.City).ThenBy(x => x.FhirServerBaseUrl).ToArray();
    }

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
