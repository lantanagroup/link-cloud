using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.SerDes;
using System.Net;
using System.Reflection;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Automation;

public class MeasureLoader
{
    private readonly MeasureEvalServiceClient _measureEvalClient;
    private readonly ValidationServiceClient _validationClient;
    private readonly IAutomationOutput _output;
    private readonly TestScenarioConfig _config;
    private readonly Assembly? _resourceAssembly;
    private readonly FhirJsonParser _parser = LinkFhirSerializerOptions.FhirJsonParserPermissive;

    public string? MeasureId;
    private Bundle? _evaluationBundle;
    private Bundle? _validationBundle;

    public MeasureLoader(
        MeasureEvalServiceClient measureEvalClient,
        ValidationServiceClient validationClient,
        IAutomationOutput output,
        TestScenarioConfig config,
        Assembly? resourceAssembly = null)
    {
        _measureEvalClient = measureEvalClient;
        _validationClient = validationClient;
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
            using var client = new HttpClient();
            return await client.GetStringAsync(_config.MeasureBundleLocation);
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
        var putStatus = await _measureEvalClient.PutMeasureDefinitionAsync(this._evaluationBundle!.ToJson());

        if (putStatus != HttpStatusCode.OK)
        {
            _output.WriteLine($"Failed to load measure definition: HTTP {putStatus}");
            throw new Exception("Failed to load measure definition.");
        }

        await VerifyMeasureDefinitionAsync();

        if (this._validationBundle != null)
        {
            _output.WriteLine("Loading profile artifacts for validation...");

            var validationTasks = this._validationBundle.Entry.Select(async validationEntry =>
            {
                var resource = validationEntry.Resource!;
                var artifactId = $"{resource.TypeName}-{resource.Id}";
                var status = await _validationClient.UpsertResourceArtifactAsync(artifactId, await resource.ToJsonAsync());

                if (status != HttpStatusCode.OK)
                {
                    _output.WriteLine($"Failed to load validation resource '{artifactId}': HTTP {status}");
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
            var (status, content) = await _measureEvalClient.GetMeasureDefinitionAsync(MeasureId!);

            if (status == HttpStatusCode.OK && content != null)
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(content);
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
                _output.WriteLine($"  WARNING: Could not verify measure definition (HTTP {status})");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"  WARNING: Measure definition verification failed: {ex.Message}");
        }
    }
}
