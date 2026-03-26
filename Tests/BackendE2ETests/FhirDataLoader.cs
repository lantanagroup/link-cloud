using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests;

using System.Reflection;
using System.Text.Json.Nodes;
using RestSharp;

public class FhirDataLoader
{
    private readonly List<string> _createdResources = new List<string>();
    private string? _authorization;
    private readonly RestClient _restClient;

    private const int MaxRetries = 3;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);

    public FhirDataLoader(string fhirServerBaseUrl)
    {
        this._restClient = new RestClient(fhirServerBaseUrl.TrimEnd('/'));
        this.GetAuthorization();
    }

    private void GetAuthorization()
    {
        if (!TestConfig.FhirServerOAuth.ShouldAuthenticate &&
            !TestConfig.FhirServerBasicAuth.ShouldAuthenticate) return;

        Console.WriteLine("Authenticating to load data on FHIR server...");

        if (TestConfig.FhirServerOAuth.ShouldAuthenticate)
        {
            this._authorization = "Bearer " + AuthHelper.GetBearerToken(TestConfig.FhirServerOAuth);
        }
        else if (TestConfig.FhirServerBasicAuth.ShouldAuthenticate)
        {
            this._authorization = "Basic " + AuthHelper.GetBasicAuthorization(TestConfig.FhirServerBasicAuth);
        }
    }

    /// <summary>
    /// Waits for the FHIR server to respond to a metadata request.
    /// Should be called before any bundle upload to avoid burning per-bundle
    /// retries on a server that hasn't started yet.
    /// </summary>
    public async Task WaitForServerAsync(ITestOutputHelper output, TimeSpan? timeout = null)
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
                var response = await this._restClient.ExecuteAsync(request);

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

    public async Task LoadEmbeddedTransactionBundles(ITestOutputHelper output)
    {
        output.WriteLine("Loading data onto FHIR server...");
        var assembly = Assembly.GetExecutingAssembly();
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

                            if (!this._createdResources.Contains(resourcePath))
                                this._createdResources.Add(resourcePath);
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

    public void DeleteResourcesWithExpunge(ITestOutputHelper output)
    {
        output.WriteLine("Removing data from FHIR server...");

        foreach (var resource in this._createdResources)
        {
            var request = new RestRequest($"{resource}", Method.Delete);
            request.AddHeader("Content-Type", "application/fhir+json");

            if (!string.IsNullOrEmpty(this._authorization))
                request.AddHeader("Authorization", this._authorization);

            request.AddQueryParameter("_expunge", "true");

            var response = this._restClient.Execute(request);

            output.WriteLine($"Expunging {resource} => Status: {response.StatusCode}");

            if (!response.IsSuccessful)
            {
                output.WriteLine($"Failed to expunge {resource}: {response.Content}");
            }
        }
    }

    public void ExpungeEverything(ITestOutputHelper output)
    {
        output.WriteLine("Removing data from FHIR server...");

        var request = new RestRequest("$expunge", Method.Post);
        request.AddHeader("Content-Type", "application/fhir+json");

        if (!string.IsNullOrEmpty(this._authorization))
            request.AddHeader("Authorization", this._authorization);

        string body = """
            {
              "resourceType": "Parameters",
              "parameter": [
                { "name": "expungeEverything", "valueBoolean": true }
              ]
            }
            """;
        request.AddStringBody(body, DataFormat.Json);

        var response = this._restClient.Execute(request);

        output.WriteLine($"Expunging everything => Status: {response.StatusCode}");
        if (!response.IsSuccessful)
        {
            output.WriteLine($"Failed to expunge everything: {response.Content}");
        }
    }

    /// <summary>
    /// Loads pre-built FHIR transaction bundle JSON strings onto the FHIR server.
    /// Used by tests that generate bundles at runtime (e.g., MegaPatientAdhocReportingTest).
    /// </summary>
    public async Task LoadTransactionBundlesFromJsonAsync(
        ITestOutputHelper output,
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
                            output.WriteLine("Failed response for index " + entries.IndexOf(entry) + ": " + responseNode);
                        }

                        if (!string.IsNullOrEmpty(location))
                        {
                            var resourcePath = location.Split("/_history")[0];

                            if (!this._createdResources.Contains(resourcePath))
                                this._createdResources.Add(resourcePath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                output.WriteLine("Error parsing response for " + name + ": " + ex.Message);
            }
        }

        output.WriteLine($"Upload complete: {successCount} succeeded, {failCount} failed out of {bundles.Count} bundles.");
    }

    /// <summary>
    /// Posts a FHIR transaction bundle with retry logic to handle transient
    /// connection failures (e.g., FHIR server still starting up).
    /// Uses exponential backoff: 2s, 4s, 8s.
    /// </summary>
    private async Task<RestResponse> PostBundleWithRetryAsync(
        string bundleJson, string name, string progress, ITestOutputHelper output)
    {
        var delay = InitialRetryDelay;
        RestResponse? lastResponse = null;

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            var request = new RestRequest("", Method.Post);
            request.AddHeader("Content-Type", "application/fhir+json");

            if (!string.IsNullOrEmpty(this._authorization))
                request.AddHeader("Authorization", this._authorization);

            request.AddStringBody(bundleJson, DataFormat.Json);

            lastResponse = await this._restClient.ExecuteAsync(request);

            if (lastResponse.IsSuccessful)
            {
                if (attempt > 1)
                    output.WriteLine($"  {progress} Posted {name} => {lastResponse.StatusCode} (succeeded on attempt {attempt})");
                else
                    output.WriteLine($"  {progress} Posted {name} => {lastResponse.StatusCode}");
                return lastResponse;
            }

            // Status 0 = connection refused / timeout, worth retrying
            // 5xx = server error, worth retrying
            // Anything else (4xx) is not transient
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
