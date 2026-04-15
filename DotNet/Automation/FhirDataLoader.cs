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
    private readonly OAuthConfig? _oauthConfig;
    private readonly BasicAuthConfig? _basicAuthConfig;

    private const int MaxRetries = 3;
    private const int MaxConcurrentUploads = 4;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);

    public FhirDataLoader(string fhirServerBaseUrl, OAuthConfig? oauthConfig = null, BasicAuthConfig? basicAuthConfig = null)
    {
        _oauthConfig = oauthConfig;
        _basicAuthConfig = basicAuthConfig;
        _restClient = new RestClient(fhirServerBaseUrl.TrimEnd('/'));
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
        var allSucceeded = true;

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
                allSucceeded = false;
                continue;
            }

            TrackCreatedResources(response.Content, name, progress, output);
        }

        return allSucceeded;
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
