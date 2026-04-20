using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.SerDes;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Automation.Link;

public class MeasureLoader
{
    private readonly IMeasureEvalServiceClient _measureEvalClient;
    private readonly IValidationServiceClient _validationClient;
    private readonly IAutomationOutput _output;
    private readonly TestScenarioConfig _config;
    private readonly Assembly? _resourceAssembly;
    private readonly FhirJsonParser _parser = LinkFhirSerializerOptions.FhirJsonParserPermissive;

    public string? MeasureId { get; private set; }

    /// <summary>
    /// After <see cref="LoadAllAsync"/> completes, contains the measure IDs
    /// for every loaded measure bundle (in order).
    /// </summary>
    public List<string> MeasureIds { get; } = [];

    private Bundle? _evaluationBundle;
    private Bundle? _validationBundle;

    public MeasureLoader(
        IMeasureEvalServiceClient measureEvalClient,
        IValidationServiceClient validationClient,
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

            // Resolve the assembly that contains the embedded resource.
            // The Automation project owns the measure bundles; use its assembly
            // (via ProfiledMeasureCatalog as an anchor type) by default.
            var assembly = _resourceAssembly ?? typeof(ProfiledMeasureCatalog).Assembly;
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
        await _measureEvalClient.PutMeasureDefinitionAsync(this._evaluationBundle!.ToJson());

        await VerifyMeasureDefinitionAsync();

        if (this._validationBundle != null)
        {
            _output.WriteLine("Loading profile artifacts for validation...");

            var validationTasks = this._validationBundle.Entry.Select(async validationEntry =>
            {
                var resource = validationEntry.Resource!;
                var artifactId = $"{resource.TypeName}-{resource.Id}";
                await _validationClient.UpsertResourceArtifactAsync(artifactId, await resource.ToJsonAsync());
            });

            await Task.WhenAll(validationTasks);
            _output.WriteLine($"{this._validationBundle.Entry.Count} validation resources successfully loaded.");
        }
    }

    private async Task VerifyMeasureDefinitionAsync()
    {
        try
        {
            var content = await _measureEvalClient.GetMeasureDefinitionAsync(MeasureId!);
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
        catch (Exception ex)
        {
            _output.WriteLine($"  WARNING: Measure definition verification failed: {ex.Message}");
        }
    }



    /// <summary>
    /// Loads all measure bundles referenced by the scenario config.
    /// Populates <see cref="MeasureId"/> with the first and <see cref="MeasureIds"/>
    /// with every loaded measure ID.
    /// </summary>
    public async Task LoadAllAsync()
    {
        var locations = _config.AllMeasureBundleLocations;
        if (locations.Count == 0)
            throw new InvalidOperationException("No measure bundle locations configured.");

        // Load the primary bundle via existing flow
        await LoadAsync();
        if (MeasureId != null && !MeasureIds.Contains(MeasureId))
            MeasureIds.Add(MeasureId);

        // Load additional bundles (skip the first since LoadAsync already handled it)
        for (var i = 1; i < locations.Count; i++)
        {
            var originalLocation = _config.MeasureBundleLocation;
            try
            {
                _config.MeasureBundleLocation = locations[i];
                _output.WriteLine($"Loading additional measure bundle [{i + 1}/{locations.Count}]: {locations[i]}");
                await LoadAsync();
                if (MeasureId != null && !MeasureIds.Contains(MeasureId))
                    MeasureIds.Add(MeasureId);
            }
            finally
            {
                _config.MeasureBundleLocation = originalLocation;
            }
        }

        // Restore MeasureId to the first
        MeasureId = MeasureIds.Count > 0 ? MeasureIds[0] : null;
        _output.WriteLine($"Loaded {MeasureIds.Count} measure(s): [{string.Join(", ", MeasureIds)}]");
    }
}
