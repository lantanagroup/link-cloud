using Flurl.Http;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.SerDes;
using System.Text.RegularExpressions;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Automation.Link;

public class MeasureLoader
{
    private readonly IMeasureEvalServiceClient _measureEvalClient;
    private readonly IValidationServiceClient _validationClient;
    private readonly IAutomationOutput _output;
    private readonly TestScenarioConfig _config;
    private readonly FhirJsonDeserializer _parser = LinkFhirSerializerOptions.FhirJsonDeserializerPermissive;

    public string? MeasureId { get; private set; }

    /// <summary>
    /// After <see cref="LoadAllAsync"/> completes, contains the measure IDs
    /// for every loaded measure bundle (in order).
    /// </summary>
    public List<string> MeasureIds { get; } = [];

    private Bundle? _evaluationBundle;
    private Bundle? _validationBundle;
    private string? _measureDefinitionId;

    public MeasureLoader(
        IMeasureEvalServiceClient measureEvalClient,
        IValidationServiceClient validationClient,
        IAutomationOutput output,
        TestScenarioConfig config)
    {
        _measureEvalClient = measureEvalClient;
        _validationClient = validationClient;
        _output = output;
        _config = config;
    }

    private string? _inlineBundleJson;

    private async Task<string> GetMeasureBundleJsonAsync()
    {
        if (!string.IsNullOrWhiteSpace(_inlineBundleJson))
            return _inlineBundleJson;

        if (_config.MeasureBundleLocation.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            var filePath = _config.MeasureBundleLocation.Replace("file://", "", StringComparison.OrdinalIgnoreCase);
            return await File.ReadAllTextAsync(filePath);
        }

        if (_config.MeasureBundleLocation.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
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
        this._measureDefinitionId = originalBundle.Id;

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
        // The MeasureEval service's PUT /measure-definition handler is not
        // safe against concurrent writers for the same Bundle.id: multiple
        // E2E tests using the same measure bundle will race on the shared
        // validator + evaluator cache + Mongo upsert and one will come back
        // as HTTP 500. We serialize PUTs per measure id across *processes*
        // via a file lock so parallel test runs can share the stack safely.
        var lockId = this._measureDefinitionId ?? this.MeasureId
            ?? throw new InvalidOperationException("Measure bundle did not include a Bundle.id or Measure.id.");

        await WithCrossProcessMeasureIdLockAsync(lockId, async () =>
        {
            await PutMeasureDefinitionWithRetryAsync();
        });

        await VerifyMeasureDefinitionAsync();

        if (this._validationBundle != null)
        {
            _output.WriteLine("Loading profile artifacts for validation...");

            var validationTasks = this._validationBundle.Entry.Select(async validationEntry =>
            {
                var resource = validationEntry.Resource!;
                var artifactId = $"{resource.TypeName}-{resource.Id}";
                await _validationClient.UpsertResourceArtifactAsync(artifactId, new FhirJsonSerializer().SerializeToString(resource));
            });

            await Task.WhenAll(validationTasks);
            _output.WriteLine($"{this._validationBundle.Entry.Count} validation resources successfully loaded.");
        }
    }

    /// <summary>
    /// Serializes the given action across processes on the same host, keyed on
    /// the measure id. Uses an exclusive file lock under the OS temp directory
    /// so parallel `dotnet test` runs against a shared services stack don't
    /// issue simultaneous PUTs for the same Bundle.id.
    /// </summary>
    private async Task WithCrossProcessMeasureIdLockAsync(string measureId, Func<Task> action)
    {
        var safeId = Regex.Replace(measureId, "[^A-Za-z0-9_.-]", "_");
        var lockPath = Path.Combine(Path.GetTempPath(), $"link-measure-def-{safeId}.lock");

        FileStream? lockStream = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var attempt = 0;
        while (lockStream == null)
        {
            try
            {
                lockStream = new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    options: FileOptions.DeleteOnClose);
            }
            catch (IOException)
            {
                attempt++;
                if (sw.Elapsed > TimeSpan.FromMinutes(5))
                    throw new TimeoutException(
                        $"Timed out waiting for cross-process measure-definition lock at '{lockPath}'.");
                if (attempt == 1)
                    _output.WriteLine($"  Waiting on measure-definition lock for '{measureId}'...");
                await Task.Delay(250);
            }
        }

        try
        {
            await action();
        }
        finally
        {
            lockStream.Dispose();
        }
    }

    private async Task VerifyMeasureDefinitionAsync()
    {
        try
        {
            var definitionId = _measureDefinitionId ?? MeasureId;
            if (string.IsNullOrWhiteSpace(definitionId))
                return;

            var content = await _measureEvalClient.GetMeasureDefinitionAsync(definitionId);
            if (!content.IsSuccessStatusCode || string.IsNullOrWhiteSpace(content.Body))
                throw new InvalidOperationException($"Measure definition '{definitionId}' was not found after load.");

            var json = Newtonsoft.Json.Linq.JObject.Parse(content.Body);
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
            throw;
        }
    }

    private async Task PutMeasureDefinitionWithRetryAsync()
    {
        const int maxAttempts = 5;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var response = await _measureEvalClient.PutMeasureDefinitionAsync(this._evaluationBundle!.ToJson());

            if (response.IsSuccessStatusCode)
                return;

            if (response.StatusCode is 500 or 502 or 503 or 504)
            {
                if (await MeasureDefinitionExistsAsync())
                {
                    _output.WriteLine($"  Measure definition PUT returned {response.StatusCode}, but definition already exists. Continuing.");
                    return;
                }

                if (attempt == maxAttempts)
                    throw new InvalidOperationException(
                        $"Measure definition PUT failed after {maxAttempts} attempts. HTTP {response.StatusCode}: {response.RawBody ?? "(no body)"}");

                var delay = TimeSpan.FromMilliseconds(Math.Min(4000, 250 * (1 << (attempt - 1))));
                _output.WriteLine($"  Measure definition PUT attempt {attempt}/{maxAttempts} failed with status {response.StatusCode}. Retrying in {delay.TotalMilliseconds:0} ms...");
                await Task.Delay(delay);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Measure definition PUT failed with HTTP {response.StatusCode}: {response.RawBody ?? "(no body)"}");
            }
        }
    }

    private async Task<bool> MeasureDefinitionExistsAsync()
    {
        var definitionId = _measureDefinitionId ?? MeasureId;
        if (string.IsNullOrWhiteSpace(definitionId))
            return false;

        var content = await _measureEvalClient.GetMeasureDefinitionAsync(definitionId);
        return content.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(content.Body);
    }



    /// <summary>
    /// Loads all measure bundles referenced by the scenario config.
    /// Populates <see cref="MeasureId"/> with the first and <see cref="MeasureIds"/>
    /// with every loaded measure ID.
    /// </summary>
    public async Task LoadAllAsync()
    {
        if (_config.MeasureBundleJsons.Count > 0)
        {
            foreach (var json in _config.MeasureBundleJsons)
            {
                if (string.IsNullOrWhiteSpace(json))
                    continue;

                _inlineBundleJson = json;
                _output.WriteLine($"Loading inline measure bundle [{MeasureIds.Count + 1}/{_config.MeasureBundleJsons.Count}]");
                await LoadAsync();
                if (MeasureId != null && !MeasureIds.Contains(MeasureId))
                    MeasureIds.Add(MeasureId);
            }

            _inlineBundleJson = null;
            MeasureId = MeasureIds.Count > 0 ? MeasureIds[0] : null;
            if (MeasureIds.Count == 0)
                throw new InvalidOperationException("No measure bundles configured.");
            _output.WriteLine($"Loaded {MeasureIds.Count} measure(s): [{string.Join(", ", MeasureIds)}]");
            return;
        }

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
