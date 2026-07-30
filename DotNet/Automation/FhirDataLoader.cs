using LantanaGroup.Automation.Configuration;
using LantanaGroup.Automation.Helpers;
using System.Collections.Concurrent;

namespace LantanaGroup.Automation;

using System.Reflection;
using System.Text.Json.Nodes;
using RestSharp;

/// <summary>
/// Generic FHIR data loader that interacts with a FHIR server via REST.
/// Does not depend on any Link SDK types.
/// </summary>
public class FhirDataLoader
{
    private readonly ConcurrentBag<string> _createdResources = new();
    private string? _authorization;
    private readonly RestClient _restClient;
    /// <summary>
    /// Configured FHIR server base. Captured at construction time so that paging
    /// follow-up requests can be validated against the origin we authenticated to,
    /// and the bearer/basic credential is never forwarded to a different host.
    /// </summary>
    private readonly Uri _baseUri;
    private readonly Uri _baseUriForRelativeResolution;
    private readonly OAuthConfig? _oauthConfig;
    private readonly BasicAuthConfig? _basicAuthConfig;

    private const int MaxRetries = 3;
    private const int MaxConcurrentUploads = 4;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);

    public FhirDataLoader(string fhirServerBaseUrl, OAuthConfig? oauthConfig = null, BasicAuthConfig? basicAuthConfig = null)
    {
        _oauthConfig = oauthConfig;
        _basicAuthConfig = basicAuthConfig;
        var trimmed = fhirServerBaseUrl.TrimEnd('/');
        _baseUri = new Uri(trimmed, UriKind.Absolute);
        _baseUriForRelativeResolution = EnsureTrailingSlash(_baseUri);
        _restClient = new RestClient(trimmed);
        GetAuthorization();
    }

    private void GetAuthorization()
    {
        if (_oauthConfig?.ShouldAuthenticate != true &&
            _basicAuthConfig?.ShouldAuthenticate != true) return;

        Console.WriteLine("Authenticating to load data on FHIR server...");

        if (_oauthConfig?.ShouldAuthenticate == true)
        {
            _authorization = "Bearer " + AuthHelper.GetBearerToken(_oauthConfig);
        }
        else if (_basicAuthConfig?.ShouldAuthenticate == true)
        {
            _authorization = "Basic " + AuthHelper.GetBasicAuthorization(_basicAuthConfig);
        }
    }

    /// <summary>
    /// Waits for the FHIR server to respond to a metadata request.
    /// </summary>
    public async Task WaitForServerAsync(IAutomationOutput output, TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? TimeSpan.FromSeconds(60);
        var start = DateTime.UtcNow;
        var attempt = 0;

        output.WriteLine("Waiting for FHIR server to be ready...");

        while (DateTime.UtcNow - start < maxWait)
        {
            attempt++;
            try
            {
                var request = new RestRequest("metadata", Method.Get);
                var response = await _restClient.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    output.WriteLine($"FHIR server ready (attempt {attempt}, {(DateTime.UtcNow - start).TotalSeconds:F1}s)");
                    return;
                }

                output.WriteLine($"FHIR server not ready: {response.StatusCode} (attempt {attempt}, retrying...)");
            }
            catch (Exception ex)
            {
                output.WriteLine($"FHIR server not reachable: {ex.Message} (attempt {attempt}, retrying...)");
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        output.WriteLine($"WARNING: FHIR server did not become ready within {maxWait.TotalSeconds}s. Proceeding anyway...");
    }

    /// <summary>
    /// Loads embedded FHIR transaction bundles from the specified assembly.
    /// The assembly should contain embedded resources matching the pattern ".fhir_server_data.*.json".
    /// </summary>
    public async Task LoadEmbeddedTransactionBundles(IAutomationOutput output, Assembly? resourceAssembly = null)
    {
        output.WriteLine("Loading data onto FHIR server...");
        var assembly = resourceAssembly ?? Assembly.GetCallingAssembly();
        var resourceNames = assembly.GetManifestResourceNames()
                                    .Where(name => name.Contains(".fhir_server_data.") && name.EndsWith(".json"));

        var resourceList = resourceNames.ToList();
        var shortNames = resourceList
            .Select(n => n.Split(".fhir_server_data.").LastOrDefault() ?? n)
            .ToList();

        output.WriteLine($"Found {resourceList.Count} FHIR bundles to load:");
        foreach (var name in shortNames)
        {
            output.WriteLine($"  - {name}");
        }

        foreach (var resourceName in resourceList)
        {
            await using var stream = assembly.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream ?? throw new InvalidOperationException());
            var bundleJson = await reader.ReadToEndAsync();

            var shortName = resourceName.Split(".fhir_server_data.").LastOrDefault() ?? resourceName;
            var response = await PostBundleWithRetryAsync(bundleJson, shortName, "", output);

            if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
            {
                output.WriteLine("Failed response: " + response.Content);
                continue;
            }

            try
            {
                var json = JsonNode.Parse(response.Content)?.AsObject();
                var entries = json?["entry"]?.AsArray();

                if (entries != null)
                {
                    foreach (var entry in entries)
                    {
                        var responseNode = entry?["response"]?.AsObject();
                        var location = responseNode?["location"]?.ToString();
                        var status = responseNode?["status"]?.ToString();

                        if (status == null || !status.StartsWith("20"))
                        {
                            output.WriteLine("Failed response for index " + entries.IndexOf(entry) + ": " + responseNode);
                        }

                        if (!string.IsNullOrEmpty(location))
                        {
                            var resourcePath = location.Split("/_history")[0];
                            _createdResources.Add(resourcePath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                output.WriteLine("Error parsing response for " + resourceName + ": " + ex.Message);
            }
        }
    }

    public void DeleteResourcesWithExpunge(IAutomationOutput output)
    {
        var total = _createdResources.Count;
        output.WriteLine($"Expunging {total} tracked resource(s) from FHIR server...");

        var succeeded = 0;
        var failed = 0;
        var failures = new List<string>();

        foreach (var resource in _createdResources)
        {
            var request = new RestRequest($"{resource}", Method.Delete);
            request.AddHeader("Content-Type", "application/fhir+json");

            if (!string.IsNullOrEmpty(_authorization))
                request.AddHeader("Authorization", _authorization);

            request.AddQueryParameter("_expunge", "true");

            var response = _restClient.Execute(request);

            if (response.IsSuccessful)
            {
                succeeded++;
            }
            else
            {
                failed++;
                if (failures.Count < 5)
                    failures.Add($"{resource}: {response.StatusCode}");
            }
        }

        if (failed == 0)
        {
            output.WriteLine($"FHIR expunge complete: {succeeded}/{total} resource(s) removed.");
        }
        else
        {
            output.WriteLine($"FHIR expunge finished with errors: {succeeded} succeeded, {failed} failed out of {total}.");
            foreach (var f in failures)
                output.WriteLine($"  {f}");
            if (failed > failures.Count)
                output.WriteLine($"  ... and {failed - failures.Count} more.");
        }
    }

    public void ExpungeEverything(IAutomationOutput output)
    {
        output.WriteLine("Removing data from FHIR server...");

        var request = new RestRequest("$expunge", Method.Post);
        request.AddHeader("Content-Type", "application/fhir+json");

        if (!string.IsNullOrEmpty(_authorization))
            request.AddHeader("Authorization", _authorization);

        string body = """
            {
              "resourceType": "Parameters",
              "parameter": [
                { "name": "expungeEverything", "valueBoolean": true }
              ]
            }
            """;
        request.AddStringBody(body, DataFormat.Json);

        var response = _restClient.Execute(request);

        output.WriteLine($"Expunging everything => Status: {response.StatusCode}");
        if (!response.IsSuccessful)
        {
            output.WriteLine($"Failed to expunge everything: {response.Content}");
        }
    }

    /// <summary>
    /// Loads pre-built FHIR transaction bundle JSON strings onto the FHIR server.
    /// Used by tests that generate bundles at runtime.
    /// </summary>
    public async Task LoadTransactionBundlesFromJsonAsync(
        IAutomationOutput output,
        IReadOnlyList<(string Name, string Json)> bundles)
    {
        output.WriteLine($"Loading {bundles.Count} generated bundles onto FHIR server (concurrency={MaxConcurrentUploads})...");

        var successCount = 0;
        var failCount = 0;
        var completed = 0;
        var semaphore = new SemaphoreSlim(MaxConcurrentUploads, MaxConcurrentUploads);

        var tasks = bundles.Select(async (bundle, index) =>
        {
            await semaphore.WaitAsync();
            try
            {
                var (name, json) = bundle;
                var progress = $"[{Interlocked.Increment(ref completed)}/{bundles.Count}]";
                var response = await PostBundleWithRetryAsync(json, name, progress, output);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                {
                    Interlocked.Increment(ref failCount);
                    output.WriteLine($"  {progress} FAILED {name}: {response.StatusCode} {response.Content}");
                    return;
                }

                Interlocked.Increment(ref successCount);
                TrackCreatedResources(response.Content, name, progress, output);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        output.WriteLine($"Upload complete: {successCount} succeeded, {failCount} failed out of {bundles.Count} bundles.");
    }

    /// <summary>
    /// Uploads a sequence of FHIR transaction bundles in strict order. Each bundle is
    /// posted and confirmed before the next is sent. Used by the generation pipeline to
    /// ensure a patient's resources reach the FHIR server in the correct dependency order
    /// (Patient ? Encounter ? Observations, etc.) and to avoid data-shape corruption
    /// from out-of-order concurrent uploads within a single patient context.
    /// </summary>
    public async Task<bool> UploadBundlesSequentiallyAsync(
        IAutomationOutput output,
        IReadOnlyList<(string Name, string Json)> bundles,
        string progressPrefix = "")
    {
        for (var i = 0; i < bundles.Count; i++)
        {
            var (name, json) = bundles[i];
            var progress = string.IsNullOrEmpty(progressPrefix)
                ? $"[{i + 1}/{bundles.Count}]"
                : $"{progressPrefix}[{i + 1}/{bundles.Count}]";

            var response = await PostBundleWithRetryAsync(json, name, progress, output);

            if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
            {
                output.WriteLine($"  {progress} FAILED {name}: {response.StatusCode} {response.Content}");

                // Abort the sequence — later bundles depend on earlier ones.
                for (var j = i + 1; j < bundles.Count; j++)
                {
                    var skippedProgress = string.IsNullOrEmpty(progressPrefix)
                        ? $"[{j + 1}/{bundles.Count}]"
                        : $"{progressPrefix}[{j + 1}/{bundles.Count}]";
                    output.WriteLine($"  {skippedProgress} SKIPPED {bundles[j].Name} (dependency failed)");
                }

                return false;
            }

            TrackCreatedResources(response.Content, name, progress, output);
        }

        return true;
    }

    private void TrackCreatedResources(string responseContent, string name, string progress, IAutomationOutput output)
    {
        try
        {
            var jsonNode = JsonNode.Parse(responseContent)?.AsObject();
            var entries = jsonNode?["entry"]?.AsArray();

            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    var responseNode = entry?["response"]?.AsObject();
                    var location = responseNode?["location"]?.ToString();
                    var status = responseNode?["status"]?.ToString();

                    if (status == null || !status.StartsWith("20"))
                    {
                        output.WriteLine($"  {progress} Entry error in {name}: {responseNode}");
                    }

                    if (!string.IsNullOrEmpty(location))
                    {
                        var resourcePath = location.Split("/_history")[0];
                        _createdResources.Add(resourcePath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            output.WriteLine($"  {progress} Error parsing response for {name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Fetches a patient and their related resources from the FHIR server using the
    /// <c>Patient/{id}/$everything</c> operation and follows <c>Bundle.link[next]</c>
    /// pages until the server stops emitting one. Returns a single merged FHIR
    /// <c>Bundle</c> JSON string (the first page's envelope, with the union of every
    /// page's <c>entry</c> array). Used to materialize imported patients (referenced
    /// by ID) into bundle entries that can be fed through the manifest / simulator
    /// paths.
    ///
    /// <para>
    /// Pagination is non-negotiable for real-world servers (Cerner / Epic / HAPI
    /// sandboxes default to 20–100 entries per page). Without this, the simulator
    /// only sees the first page of the patient's data and predicts <c>expected=0</c>
    /// for every resource type that spilled past the page boundary, while the
    /// production DA pipeline — which queries the server directly per resource type
    /// — sees everything. That manifests as the validator complaining
    /// <c>"expected=0, actual=N"</c> for Observation / ServiceRequest / Procedure /
    /// etc.
    /// </para>
    /// </summary>
    public async Task<string> FetchPatientEverythingAsync(string patientId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(patientId))
            throw new ArgumentException("Patient ID is required.", nameof(patientId));

        var currentPageRequestUri = new Uri(_baseUriForRelativeResolution, $"Patient/{patientId}/$everything");

        // Page 1: anchored at the operation URL relative to the configured server base.
        var firstPage = await GetPageAsync(
            $"Patient/{patientId}/$everything",
            useFullUrl: false,
            descriptionForError: $"Patient/{patientId}/$everything",
            ct);

        var rootBundle = JsonNode.Parse(firstPage)
            ?? throw new InvalidOperationException(
                $"FHIR server returned an unparseable response for Patient/{patientId}/$everything.");

        var mergedEntries = (rootBundle["entry"] as JsonArray) ?? new JsonArray();

        // Capture the next link, then strip the entire `link` block from the merged
        // bundle. The first page's link block describes only the first page (self/next/last)
        // and is meaningless on the merged result; leaving it in causes downstream FHIR
        // deserializers to choke on a null/empty link array shape.
        var nextUrl = ExtractNextLink(rootBundle);
        if (rootBundle is JsonObject rootObj)
            rootObj.Remove("link");

        // Defensive cap: a server that returns the same next link forever would otherwise
        // hang the run. 1000 pages × ~100 entries/page = ~100k resources is well past any
        // realistic single-patient $everything; if we hit it we want to surface it.
        const int MaxPages = 1000;
        var pageCount = 1;

        while (!string.IsNullOrWhiteSpace(nextUrl))
        {
            ct.ThrowIfCancellationRequested();

            if (++pageCount > MaxPages)
            {
                throw new InvalidOperationException(
                    $"Patient/{patientId}/$everything exceeded the {MaxPages}-page safety cap; refusing to continue.");
            }

            var validatedNextUrl = ResolveAndValidateSameOrigin(
                nextUrl!,
                currentPageRequestUri,
                $"Patient/{patientId}/$everything (page {pageCount})");

            var pageJson = await GetPageAsync(
                validatedNextUrl.AbsoluteUri,
                useFullUrl: true,
                descriptionForError: $"Patient/{patientId}/$everything (page {pageCount})",
                ct);

            // Advance only after the page request succeeds so subsequent relative links
            // are resolved against the URI that actually produced this page.
            currentPageRequestUri = validatedNextUrl;

            var pageNode = JsonNode.Parse(pageJson);
            if (pageNode?["entry"] is JsonArray pageEntries)
            {
                foreach (var entry in pageEntries.ToList())
                {
                    // Detach from the source array before appending; JsonArray.Add
                    // throws on already-parented nodes.
                    pageEntries.Remove(entry);
                    mergedEntries.Add(entry);
                }
            }

            nextUrl = pageNode != null ? ExtractNextLink(pageNode) : null;
        }

        rootBundle["entry"] = mergedEntries;
        // Reflect the merged total so consumers that read it (and humans debugging)
        // see the post-pagination count rather than the first page's count.
        rootBundle["total"] = mergedEntries.Count;

        return rootBundle.ToJsonString();
    }

    /// <summary>
    /// Performs a single GET against the FHIR server and returns the response body.
    /// When <paramref name="useFullUrl"/> is true the URL is treated as absolute
    /// (used for paging links) and is required to share scheme+host+port with
    /// <see cref="_baseUri"/>; otherwise it is resolved against the configured server
    /// base URL via the existing <see cref="_restClient"/>. The <c>Authorization</c>
    /// header is only forwarded when the request target's origin matches the configured
    /// FHIR base, so a server-supplied <c>next</c> link can never harvest the credential.
    /// </summary>
    private async Task<string> GetPageAsync(string url, bool useFullUrl, string descriptionForError, CancellationToken ct)
    {
        RestResponse response;
        if (useFullUrl)
        {
            // Defense-in-depth: callers (the paging loop) already validate via
            // ResolveAndValidateSameOrigin, but re-check here so a future caller can't
            // bypass the origin guard.
            if (!Uri.TryCreate(url, UriKind.Absolute, out var target))
            {
                throw new InvalidOperationException(
                    $"Refusing to issue request for {descriptionForError}: '{url}' is not an absolute URI.");
            }
            if (!IsSameOrigin(_baseUri, target))
            {
                throw new InvalidOperationException(
                    $"Refusing to issue request for {descriptionForError}: target origin " +
                    $"{target.GetLeftPart(UriPartial.Authority)} differs from configured FHIR base " +
                    $"{_baseUri.GetLeftPart(UriPartial.Authority)}.");
            }

            using var oneOffClient = new RestClient(url);
            var request = new RestRequest("", Method.Get);
            request.AddHeader("Accept", "application/fhir+json");
            if (!string.IsNullOrEmpty(_authorization))
                request.AddHeader("Authorization", _authorization);
            response = await oneOffClient.ExecuteAsync(request, ct);
        }
        else
        {
            var request = new RestRequest(url, Method.Get);
            request.AddHeader("Accept", "application/fhir+json");
            if (!string.IsNullOrEmpty(_authorization))
                request.AddHeader("Authorization", _authorization);
            response = await _restClient.ExecuteAsync(request, ct);
        }

        if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
        {
            throw new InvalidOperationException(
                $"FHIR server returned {(int)response.StatusCode} {response.StatusCode} for {descriptionForError}: {response.Content}");
        }

        return response.Content;
    }

    /// <summary>
    /// Resolves a Bundle <c>next</c> link returned by the FHIR server and rejects
    /// links that would cause a cross-origin request:
    /// <list type="bullet">
    ///   <item>Relative links are resolved against <see cref="_baseUri"/>.</item>
    ///   <item>Absolute links must share scheme, host, and port with <see cref="_baseUri"/>.</item>
    /// </list>
    /// Throws <see cref="InvalidOperationException"/> when the link is malformed or points
    /// to a different origin, so pagination stops with an informative error rather than
    /// silently following an attacker-controlled link (and potentially forwarding the
    /// configured Authorization header to it).
    /// </summary>
    private Uri ResolveAndValidateSameOrigin(string nextLink, Uri currentPageRequestUri, string descriptionForError)
    {
        if (!Uri.TryCreate(nextLink, UriKind.RelativeOrAbsolute, out var parsed))
        {
            throw new InvalidOperationException(
                $"Refusing to follow Bundle next link for {descriptionForError}: '{nextLink}' is not a valid URI.");
        }

        var absolute = parsed.IsAbsoluteUri ? parsed : new Uri(currentPageRequestUri, parsed);

        if (!absolute.IsAbsoluteUri)
        {
            throw new InvalidOperationException(
                $"Refusing to follow Bundle next link for {descriptionForError}: could not resolve '{nextLink}' against base '{_baseUri}'.");
        }

        if (!IsSameOrigin(_baseUri, absolute))
        {
            throw new InvalidOperationException(
                $"Refusing to follow Bundle next link for {descriptionForError}: target origin " +
                $"{absolute.GetLeftPart(UriPartial.Authority)} differs from configured FHIR base " +
                $"{_baseUri.GetLeftPart(UriPartial.Authority)}.");
        }

        return absolute;
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        if (uri.AbsolutePath.EndsWith('/'))
            return uri;

        var builder = new UriBuilder(uri)
        {
            Path = uri.AbsolutePath + "/"
        };

        return builder.Uri;
    }

    private static bool IsSameOrigin(Uri left, Uri right) =>
        string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
        && left.Port == right.Port;

    /// <summary>
    /// Returns the absolute URL of the <c>Bundle.link</c> entry whose <c>relation</c>
    /// is <c>"next"</c>, or <c>null</c> when the bundle has no further pages.
    /// </summary>
    private static string? ExtractNextLink(JsonNode bundle)
    {
        if (bundle["link"] is not JsonArray links) return null;

        foreach (var link in links)
        {
            var relation = link?["relation"]?.GetValue<string>();
            if (string.Equals(relation, "next", StringComparison.OrdinalIgnoreCase))
            {
                return link?["url"]?.GetValue<string>();
            }
        }

        return null;
    }

    private async Task<RestResponse> PostBundleWithRetryAsync(
        string bundleJson,
        string name,
        string progress,
        IAutomationOutput output)
    {
        var delay = InitialRetryDelay;
        RestResponse? lastResponse = null;

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            var request = new RestRequest("", Method.Post);
            request.AddHeader("Content-Type", "application/fhir+json");

            if (!string.IsNullOrEmpty(_authorization))
                request.AddHeader("Authorization", _authorization);

            request.AddStringBody(bundleJson, DataFormat.Json);

            lastResponse = await _restClient.ExecuteAsync(request);

            if (lastResponse.IsSuccessful)
            {
                if (attempt > 1)
                    output.WriteLine($"  {progress} Posted {name} => {lastResponse.StatusCode} (succeeded on attempt {attempt})");
                else
                    output.WriteLine($"  {progress} Posted {name} => {lastResponse.StatusCode}");
                return lastResponse;
            }

            var statusCode = (int)lastResponse.StatusCode;
            if (statusCode != 0 && statusCode < 500)
            {
                output.WriteLine($"  {progress} Posted {name} => {lastResponse.StatusCode} (non-retryable)");
                return lastResponse;
            }

            if (attempt < MaxRetries)
            {
                output.WriteLine($"  {progress} Posted {name} => {lastResponse.StatusCode} (attempt {attempt}/{MaxRetries}, retrying in {delay.TotalSeconds:F0}s...)");
                await Task.Delay(delay);
                delay *= 2;
            }
            else
            {
                output.WriteLine($"  {progress} Posted {name} => {lastResponse.StatusCode} (attempt {attempt}/{MaxRetries}, giving up)");
            }
        }

        return lastResponse!;
    }
}
