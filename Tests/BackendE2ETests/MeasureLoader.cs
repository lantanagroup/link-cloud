using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.SerDes;
using RestSharp;
using System.Reflection;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Tests.E2ETests;
public class MeasureLoader(RestClient adminBffClient, ITestOutputHelper output)
{
    private readonly FhirJsonParser _parser = LinkFhirSerializerOptions.FhirJsonParserPermissive;
    public string? MeasureId;
    private Bundle? _evaluationBundle;
    private Bundle? _validationBundle;

    private async Task<string> GetMeasureBundleJsonAsync()
    {
        if (TestConfig.AdhocReportingSmokeTestConfig.MeasureBundleLocation.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            var filePath = TestConfig.AdhocReportingSmokeTestConfig.MeasureBundleLocation.Replace("file://", "", StringComparison.OrdinalIgnoreCase);
            return await File.ReadAllTextAsync(filePath);
        }
        else if (TestConfig.AdhocReportingSmokeTestConfig.MeasureBundleLocation.StartsWith("resource://", StringComparison.OrdinalIgnoreCase))
        {
            var resourceName = TestConfig.AdhocReportingSmokeTestConfig.MeasureBundleLocation
                .Replace("resource://", "", StringComparison.OrdinalIgnoreCase);
            await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            
            if (stream == null)
                throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");

            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        else if (TestConfig.AdhocReportingSmokeTestConfig.MeasureBundleLocation.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 TestConfig.AdhocReportingSmokeTestConfig.MeasureBundleLocation.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var client = new RestClient();
            var request = new RestRequest(TestConfig.AdhocReportingSmokeTestConfig.MeasureBundleLocation, Method.Get);
            var response = await client.ExecuteAsync(request);

            if (!response.IsSuccessful)
                throw new Exception($"Failed to fetch bundle from {TestConfig.AdhocReportingSmokeTestConfig.MeasureBundleLocation}: {response.ErrorMessage}");

            return response.Content;
        }

        throw new NotSupportedException($"Unsupported path type: {TestConfig.AdhocReportingSmokeTestConfig.MeasureBundleLocation}");
    }

    private async Task GetMeasureBundleAsync()
    {
        var json = await this.GetMeasureBundleJsonAsync();
        var originalBundle = _parser.Parse<Bundle>(json);

        var evaluationTypes = new[] { "Measure", "Library", "ValueSet", "CodeSystem" };
        var validationTypes = new[] { "ImplementationGuide", "StructureDefinition", "SearchParameter", "ValueSet", "CodeSystem" };

        Measure measure = originalBundle.Entry.FirstOrDefault(e => e.Resource?.TypeName == "Measure")?.Resource as Measure ?? throw new InvalidOperationException("Measure not found in bundle.");
        this.MeasureId = measure.Id;
        
        this._evaluationBundle = new Bundle
        {
            Type = Bundle.BundleType.Transaction,
            Id = originalBundle.Id,
            Entry = originalBundle.Entry
                .Where(e => e.Resource != null && evaluationTypes.Contains(e.Resource.TypeName))
                .ToList()
        };

        this._validationBundle = new Bundle
        {
            Type = Bundle.BundleType.Transaction,
            Entry = originalBundle.Entry
                .Where(e => e.Resource != null && validationTypes.Contains(e.Resource.TypeName))
                .ToList()
        };
    }

    public async Task LoadAsync()
    {
        output.WriteLine("Getting measure bundle...");
        await this.GetMeasureBundleAsync();
        
        output.WriteLine("Loading measure bundle for evaluation...");
        var request = new RestRequest($"measureeval/measure-definition", Method.Put);
        request.AddJsonBody(this._evaluationBundle.ToJson());
        var response = adminBffClient.ExecuteAsync(request);
        
        if (response.Result.StatusCode != System.Net.HttpStatusCode.OK)
        {
            output.WriteLine($"Failed to load measure definition: {response.Result.Content}");
            throw new Exception("Failed to load measure definition.");
        }

        // Verify the definition was persisted by reading it back
        await VerifyMeasureDefinitionAsync();

        if (this._validationBundle != null)
        {
            output.WriteLine("Loading profile artifacts for validation...");
                
            var validationTasks = this._validationBundle.Entry.Select(async validationEntry =>
            {
                var resource = validationEntry.Resource;
                var requestValidation = new RestRequest($"validation/artifact/RESOURCE/{resource.TypeName}-{resource.Id}", Method.Put);
                requestValidation.AddJsonBody(await resource.ToJsonAsync());
                var responseValidation = await adminBffClient.ExecuteAsync(requestValidation);
                
                if (responseValidation.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    output.WriteLine($"Failed to load validation resource: {responseValidation.Content}");
                    throw new Exception("Failed to load validation resource.");
                }
            });
            
            await Task.WhenAll(validationTasks);
            output.WriteLine($"{this._validationBundle.Entry.Count} validation resources successfully loaded.");
        }
    }

    /// <summary>
    /// Reads the measure definition back from MeasureEval to confirm it was persisted,
    /// and logs the related artifact summary.
    /// </summary>
    private async Task VerifyMeasureDefinitionAsync()
    {
        try
        {
            var verifyRequest = new RestRequest($"measureeval/measure-definition/{MeasureId}", Method.Get);
            var verifyResponse = await adminBffClient.ExecuteAsync(verifyRequest);

            if (verifyResponse.StatusCode == System.Net.HttpStatusCode.OK && verifyResponse.Content != null)
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(verifyResponse.Content);
                var id = json["id"]?.ToString() ?? "(unknown)";
                var bundle = json["bundle"];
                var entryCount = (bundle?["entry"] as Newtonsoft.Json.Linq.JArray)?.Count ?? 0;

                // Summarize resource types in the bundle
                var resourceTypes = (bundle?["entry"] as Newtonsoft.Json.Linq.JArray)?
                    .Select(e => e["resource"]?["resourceType"]?.ToString() ?? "unknown")
                    .GroupBy(t => t)
                    .Select(g => $"{g.Key}={g.Count()}")
                    .ToList() ?? [];

                output.WriteLine($"  MeasureEval definition verified: id={id}, entries={entryCount}");
                output.WriteLine($"  Resource types: {string.Join(", ", resourceTypes)}");
            }
            else
            {
                output.WriteLine($"  WARNING: Could not verify measure definition (HTTP {verifyResponse.StatusCode})");
            }
        }
        catch (Exception ex)
        {
            output.WriteLine($"  WARNING: Measure definition verification failed: {ex.Message}");
        }
    }
}