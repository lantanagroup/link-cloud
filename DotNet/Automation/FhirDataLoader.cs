using LantanaGroup.Automation.Configuration;
using LantanaGroup.Automation.Helpers;

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
    private readonly List<string> _createdResources = new List<string>();
    private string? _authorization;
    private readonly RestClient _restClient;
    private readonly OAuthConfig? _oauthConfig;
    private readonly BasicAuthConfig? _basicAuthConfig;

    private const int MaxRetries = 3;
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

                            if (!_createdResources.Contains(resourcePath))
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
        output.WriteLine("Removing data from FHIR server...");

        foreach (var resource in _createdResources)
        {
            var request = new RestRequest($"{resource}", Method.Delete);
            request.AddHeader("Content-Type", "application/fhir+json");

            if (!string.IsNullOrEmpty(_authorization))
                request.AddHeader("Authorization", _authorization);

            request.AddQueryParameter("_expunge", "true");

            var response = _restClient.Execute(request);

            output.WriteLine($"Expunging {resource} => Status: {response.StatusCode}");

            if (!response.IsSuccessful)
            {
                output.WriteLine($"Failed to expunge {resource}: {response.Content}");
            }
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
        output.WriteLine($"Loading {bundles.Count} generated bundles onto FHIR server...");

        var successCount = 0;
        var failCount = 0;

        for (var b = 0; b < bundles.Count; b++)
        {
            var (name, json) = bundles[b];
            var progress = $"[{b + 1}/{bundles.Count}]";
            var response = await PostBundleWithRetryAsync(json, name, progress, output);

            if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
            {
                failCount++;
                output.WriteLine($"  {progress} FAILED {name}: {response.StatusCode} {response.Content}");
                continue;
            }

            successCount++;

            try
            {
                var jsonNode = JsonNode.Parse(response.Content)?.AsObject();
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
                            if (!_createdResources.Contains(resourcePath))
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

        output.WriteLine($"Bundle loading complete: {successCount} succeeded, {failCount} failed.");
    }

    private async Task<RestResponse> PostBundleWithRetryAsync(
        string bundleJson,
        string name,
        string progress,
        IAutomationOutput output)
    {
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
                    output.WriteLine($"  {progress} Retry succeeded for {name} on attempt {attempt}");
                return lastResponse;
            }

            if (attempt < MaxRetries)
            {
                var delay = InitialRetryDelay * Math.Pow(2, attempt - 1);
                output.WriteLine($"  {progress} Attempt {attempt} failed for {name}: {lastResponse.StatusCode}. Retrying in {delay.TotalSeconds:F0}s...");
                await Task.Delay(delay);
            }
        }

        return lastResponse!;
    }
}
