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
            // Get a token for the user
            this._authorization = "Bearer " + AuthHelper.GetBearerToken(TestConfig.FhirServerOAuth);
        }
        else if (TestConfig.FhirServerBasicAuth.ShouldAuthenticate)
        {
            this._authorization = "Basic " + AuthHelper.GetBasicAuthorization(TestConfig.FhirServerBasicAuth);
        }
    }
    public async Task LoadEmbeddedTransactionBundles(ITestOutputHelper output)
    {
        output.WriteLine("Loading data onto FHIR server...");
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames()
                                    .Where(name => name.Contains(".fhir_server_data.") && name.EndsWith(".json"));
        
        output.WriteLine($"Found {resourceNames.Count()} resources to load. {string.Join(", ", resourceNames)}");

        foreach (var resourceName in resourceNames)
        {
            await using var stream = assembly.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream ?? throw new InvalidOperationException());
            var bundleJson = await reader.ReadToEndAsync();

            var request = new RestRequest("", Method.Post);
            request.AddHeader("Content-Type", "application/fhir+json");
            
            if (!string.IsNullOrEmpty(this._authorization))
                request.AddHeader("Authorization", this._authorization);
            
            request.AddStringBody(bundleJson, DataFormat.Json);

            var response = await this._restClient.ExecuteAsync(request);

            output.WriteLine($"Posted {resourceName} => Status: {response.StatusCode}");

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
                        var location = responseNode?["location"]?.ToString(); // e.g., "Observation/123/_history/1"
                        var status = responseNode?["status"]?.ToString();

                        if (status == null || !status.StartsWith("20"))
                        {
                            output.WriteLine("Failed response for index " + entries.IndexOf(entry) + ": " + responseNode);
                        }

                        if (!string.IsNullOrEmpty(location))
                        {
                            var resourcePath = location.Split("/_history")[0]; // Just "Observation/123"
                            
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
}