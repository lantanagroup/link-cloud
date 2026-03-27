using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Shared.Application.SerDes;
using RestSharp;
using System.Reflection;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Automation;

public class MeasureLoader
{
    private readonly RestClient _adminBffClient;
    private readonly IAutomationOutput _output;
    private readonly TestScenarioConfig _config;
    private readonly Assembly? _resourceAssembly;
    private readonly FhirJsonParser _parser = LinkFhirSerializerOptions.FhirJsonParserPermissive;

    public string? MeasureId;
    private Bundle? _evaluationBundle;
    private Bundle? _validationBundle;

    /// <param name="adminBffClient">REST client for the Admin BFF API.</param>
    /// <param name="output">Test output helper.</param>
    /// <param name="config">The scenario config containing the measure bundle location.</param>
    /// <param name="resourceAssembly">
    /// The assembly to load embedded resources from when MeasureBundleLocation uses the "resource://" scheme.
    /// If null, defaults to the calling assembly.
    /// </param>
    public MeasureLoader(RestClient adminBffClient, IAutomationOutput output, TestScenarioConfig config, Assembly? resourceAssembly = null)
    {
        _adminBffClient = adminBffClient;
        _output = output;
        _config = config;
        _resourceAssembly = resourceAssembly;
    }

    private async Task<string> GetMeasureBundleJsonAsync()
    {
        if (_config.MeasureBundleLocation.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            var filePath = _config.MeasureBundleLocation.Replace("file://", "", StringComparison.OrdinalIgnoreCase);
            return await File.ReadAllTextAsync(filePath);
        }
        else if (_config.MeasureBundleLocation.StartsWith("resource://", StringComparison.OrdinalIgnoreCase))
        {
            var resourceName = _config.MeasureBundleLocation
                .Replace("resource://", "", StringComparison.OrdinalIgnoreCase);
            var assembly = _resourceAssembly ?? Assembly.GetExecutingAssembly();
            await using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
                throw new FileNotFoundException($"Embedded resource '{resourceName}' not found in assembly '{assembly.GetName().Name}'.");

            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        else if (_config.MeasureBundleLocation.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 _config.MeasureBundleLocation.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var client = new RestClient();
            var request = new RestRequest(_config.MeasureBundleLocation, Method.Get);
            var response = await client.ExecuteAsync(request);

            if (!response.IsSuccessful)
                throw new Exception($"Failed to fetch bundle from {_config.MeasureBundleLocation}: {response.ErrorMessage}");

            return response.Content;
        }

        throw new NotSupportedException($"Unsupported path type: {_config.MeasureBundleLocation}");
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
        _output.WriteLine("Getting measure bundle...");
        await this.GetMeasureBundleAsync();

        _output.WriteLine("Loading measure bundle for evaluation...");
        var request = new RestRequest($"measureeval/measure-definition", Method.Put);
        request.AddJsonBody(this._evaluationBundle.ToJson());
        var response = _adminBffClient.ExecuteAsync(request);

        if (response.Result.StatusCode != System.Net.HttpStatusCode.OK)
        {
            _output.WriteLine($"Failed to load measure definition: {response.Result.Content}");
            throw new Exception("Failed to load measure definition.");
        }

        // Verify the definition was persisted by reading it back
        await VerifyMeasureDefinitionAsync();

        if (this._validationBundle != null)
        {
            _output.WriteLine("Loading profile artifacts for validation...");

            var validationTasks = this._validationBundle.Entry.Select(async validationEntry =>
            {
                var resource = validationEntry.Resource;
                var requestValidation = new RestRequest($"validation/artifact/RESOURCE/{resource.TypeName}-{resource.Id}", Method.Put);
                requestValidation.AddJsonBody(await resource.ToJsonAsync());
                var responseValidation = await _adminBffClient.ExecuteAsync(requestValidation);

                if (responseValidation.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    _output.WriteLine($"Failed to load validation resource: {responseValidation.Content}");
                    throw new Exception("Failed to load validation resource.");
                }
            });

            await Task.WhenAll(validationTasks);
            _output.WriteLine($"{this._validationBundle.Entry.Count} validation resources successfully loaded.");
        }
    }

    private async Task VerifyMeasureDefinitionAsync()
    {
        try
        {
            var verifyRequest = new RestRequest($"measureeval/measure-definition/{MeasureId}", Method.Get);
            var verifyResponse = await _adminBffClient.ExecuteAsync(verifyRequest);

            if (verifyResponse.StatusCode == System.Net.HttpStatusCode.OK && verifyResponse.Content != null)
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(verifyResponse.Content);
                var id = json["id"]?.ToString() ?? "(unknown)";
                var bundle = json["bundle"];
                var entryCount = (bundle?["entry"] as Newtonsoft.Json.Linq.JArray)?.Count ?? 0;

                var resourceTypes = (bundle?["entry"] as Newtonsoft.Json.Linq.JArray)?
                    .Select(e => e["resource"]?["resourceType"]?.ToString() ?? "unknown")
                    .GroupBy(t => t)
                    .Select(g => $"{g.Key}={g.Count()}")
                    .ToList() ?? [];

                _output.WriteLine($"  MeasureEval definition verified: id={id}, entries={entryCount}");
                _output.WriteLine($"  Resource types: {string.Join(", ", resourceTypes)}");
            }
            else
            {
                _output.WriteLine($"  WARNING: Could not verify measure definition (HTTP {verifyResponse.StatusCode})");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"  WARNING: Measure definition verification failed: {ex.Message}");
        }
    }
}
