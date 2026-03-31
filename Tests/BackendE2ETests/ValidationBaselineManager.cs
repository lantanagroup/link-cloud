using System.Reflection;
using System.Text.Json;
using LantanaGroup.Link.Automation.Helpers;

namespace LantanaGroup.Link.Tests.E2ETests;

public static class ValidationBaselineManager
{
    private const string BaselineDirectoryEnvVar = "E2E_BASELINE_DIR";
    private const string BaselineRegenerateEnvVar = "E2E_BASELINE_REGENERATE";

    private sealed class ValidationBaselineDocument
    {
        public int SchemaVersion { get; set; } = 1;
        public string BaselineName { get; set; } = string.Empty;
        public string MeasureId { get; set; } = string.Empty;
        public List<string> ExpectedPatientIds { get; set; } = [];
        public SortedDictionary<string, int> GeneratedInputCountsByType { get; set; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, SortedDictionary<string, int>> DataAcquisitionCountsByPatientType { get; set; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, SortedDictionary<string, int>> MeasureEvalCountsByPatientType { get; set; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, SortedDictionary<string, int>> ReportCountsByPatientType { get; set; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, SortedDictionary<string, int>> AbsCountsByPatientType { get; set; } = new(StringComparer.Ordinal);
    }

    public static async Task ValidateOrCreateAsync(
        IAutomationOutput output,
        PipelineDataReader dataReader,
        string baselineName,
        string facilityId,
        string reportId,
        string measureId,
        IReadOnlyCollection<string> expectedPatientIds,
        IReadOnlyList<(string Name, string Json)> generatedBundles,
        IDictionary<string, object> internalAbsResources)
    {
        if (!Guid.TryParse(reportId, out var scheduleId))
            throw new InvalidOperationException($"Invalid report id for baseline validation: {reportId}");

        var current = await BuildCurrentAsync(
            baselineName,
            dataReader,
            scheduleId,
            facilityId,
            reportId,
            measureId,
            expectedPatientIds,
            generatedBundles,
            internalAbsResources);

        var baselinePath = GetBaselinePath(baselineName);
        var regenerate = bool.TryParse(Environment.GetEnvironmentVariable(BaselineRegenerateEnvVar), out var regenerateFlag) && regenerateFlag;

        var baseline = await TryLoadBaselineAsync(baselineName, baselinePath);
        if (baseline == null || regenerate)
        {
            await WriteBaselineAsync(baselinePath, current);
            output.WriteLine(baseline == null
                ? $"[BASELINE] Created baseline '{baselineName}' at {baselinePath}"
                : $"[BASELINE] Regenerated baseline '{baselineName}' at {baselinePath}");
            return;
        }

        var diffs = Compare(baseline, current);
        if (diffs.Count == 0)
        {
            output.WriteLine($"[BASELINE] Baseline matched for {baselineName}");
            return;
        }

        output.WriteLine($"[BASELINE] Baseline mismatch for {baselineName}: {diffs.Count} difference(s)");
        foreach (var diff in diffs.Take(100))
            output.WriteLine($"  - {diff}");

        if (diffs.Count > 100)
            output.WriteLine($"  - Additional differences omitted: {diffs.Count - 100}");

        var diffSummary = string.Join(Environment.NewLine, diffs.Take(20).Select(d => $"  - {d}"));
        if (diffs.Count > 20)
            diffSummary += $"{Environment.NewLine}  ... and {diffs.Count - 20} more difference(s)";

        throw new InvalidOperationException(
            $"Baseline validation failed for {baselineName}.{Environment.NewLine}" +
            $"Differences:{Environment.NewLine}{diffSummary}{Environment.NewLine}" +
            $"Committed baseline: '{baselinePath}'.");
    }

    private static async Task<ValidationBaselineDocument> BuildCurrentAsync(
        string baselineName,
        PipelineDataReader dataReader,
        Guid scheduleId,
        string facilityId,
        string reportId,
        string measureId,
        IReadOnlyCollection<string> expectedPatientIds,
        IReadOnlyList<(string Name, string Json)> generatedBundles,
        IDictionary<string, object> internalAbsResources)
    {
        var reportCounts = await dataReader.GetReportResourceCountsByPatientTypeAsync(scheduleId, facilityId);
        var measureEvalCounts = await dataReader.GetMeasureEvalResourceCountsByPatientTypeAsync(scheduleId);
        var dataAcqCounts = await dataReader.GetDataAcquisitionResourceCountsByPatientTypeAsync(facilityId, reportId);

        return new ValidationBaselineDocument
        {
            BaselineName = baselineName,
            MeasureId = measureId,
            ExpectedPatientIds = expectedPatientIds.OrderBy(x => x, StringComparer.Ordinal).ToList(),
            GeneratedInputCountsByType = GetGeneratedInputCounts(generatedBundles),
            DataAcquisitionCountsByPatientType = ToNestedMap(dataAcqCounts.Select(x => (x.PatientId, x.ResourceType, x.Count))),
            MeasureEvalCountsByPatientType = ToNestedMap(measureEvalCounts.Select(x => (x.PatientId, x.ResourceType, x.Count))),
            ReportCountsByPatientType = ToNestedMap(reportCounts.Select(x => (x.PatientId, x.ResourceType, x.Count))),
            AbsCountsByPatientType = GetAbsCountsByPatientType(internalAbsResources, expectedPatientIds)
        };
    }

    private static SortedDictionary<string, int> GetGeneratedInputCounts(IReadOnlyList<(string Name, string Json)> generatedBundles)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var (_, json) in generatedBundles)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("request", out var request) || request.ValueKind != JsonValueKind.Object)
                    continue;

                var url = request.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String
                    ? urlProp.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(url) || !url.Contains('/'))
                    continue;

                var type = url.Split('/')[0];
                counts[type] = counts.TryGetValue(type, out var c) ? c + 1 : 1;
            }
        }

        return counts;
    }

    private static SortedDictionary<string, SortedDictionary<string, int>> GetAbsCountsByPatientType(
        IDictionary<string, object> internalAbsResources,
        IReadOnlyCollection<string> expectedPatientIds)
    {
        var result = new SortedDictionary<string, SortedDictionary<string, int>>(StringComparer.Ordinal);

        foreach (var patientId in expectedPatientIds)
        {
            var fileName = $"patient-{patientId}.ndjson";
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);

            if (internalAbsResources.TryGetValue(fileName, out var obj) && obj is string ndjson)
            {
                foreach (var line in ndjson.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
                {
                    using var doc = JsonDocument.Parse(line);
                    if (!doc.RootElement.TryGetProperty("resourceType", out var typeProp) || typeProp.ValueKind != JsonValueKind.String)
                        continue;

                    var type = typeProp.GetString();
                    if (string.IsNullOrWhiteSpace(type))
                        continue;

                    counts[type] = counts.TryGetValue(type, out var c) ? c + 1 : 1;
                }
            }

            result[patientId] = counts;
        }

        return result;
    }

    private static SortedDictionary<string, SortedDictionary<string, int>> ToNestedMap(
        IEnumerable<(string PatientId, string ResourceType, int Count)> rows)
    {
        var map = new SortedDictionary<string, SortedDictionary<string, int>>(StringComparer.Ordinal);

        foreach (var row in rows.Where(r => !string.IsNullOrWhiteSpace(r.PatientId) && !string.IsNullOrWhiteSpace(r.ResourceType)))
        {
            if (!map.TryGetValue(row.PatientId, out var perType))
            {
                perType = new SortedDictionary<string, int>(StringComparer.Ordinal);
                map[row.PatientId] = perType;
            }

            perType[row.ResourceType] = perType.TryGetValue(row.ResourceType, out var existing)
                ? existing + row.Count
                : row.Count;
        }

        return map;
    }

    private static List<string> Compare(ValidationBaselineDocument expected, ValidationBaselineDocument actual)
    {
        var diffs = new List<string>();

        if (!string.Equals(expected.MeasureId, actual.MeasureId, StringComparison.Ordinal))
            diffs.Add($"MeasureId mismatch: expected={expected.MeasureId}, actual={actual.MeasureId}");

        if (!expected.ExpectedPatientIds.SequenceEqual(actual.ExpectedPatientIds, StringComparer.Ordinal))
            diffs.Add("ExpectedPatientIds mismatch.");

        CompareFlatCounts("GeneratedInputCountsByType", expected.GeneratedInputCountsByType, actual.GeneratedInputCountsByType, diffs);
        CompareNestedCounts("DataAcquisitionCountsByPatientType", expected.DataAcquisitionCountsByPatientType, actual.DataAcquisitionCountsByPatientType, diffs);
        CompareNestedCounts("MeasureEvalCountsByPatientType", expected.MeasureEvalCountsByPatientType, actual.MeasureEvalCountsByPatientType, diffs);
        CompareNestedCounts("ReportCountsByPatientType", expected.ReportCountsByPatientType, actual.ReportCountsByPatientType, diffs);
        CompareNestedCounts("AbsCountsByPatientType", expected.AbsCountsByPatientType, actual.AbsCountsByPatientType, diffs);

        return diffs;
    }

    private static void CompareFlatCounts(
        string label,
        SortedDictionary<string, int> expected,
        SortedDictionary<string, int> actual,
        List<string> diffs)
    {
        foreach (var key in expected.Keys.Union(actual.Keys, StringComparer.Ordinal))
        {
            expected.TryGetValue(key, out var e);
            actual.TryGetValue(key, out var a);
            if (e != a)
                diffs.Add($"{label} type='{key}' expected={e}, actual={a}");
        }
    }

    private static void CompareNestedCounts(
        string label,
        SortedDictionary<string, SortedDictionary<string, int>> expected,
        SortedDictionary<string, SortedDictionary<string, int>> actual,
        List<string> diffs)
    {
        foreach (var patientId in expected.Keys.Union(actual.Keys, StringComparer.Ordinal))
        {
            expected.TryGetValue(patientId, out var expectedPerType);
            actual.TryGetValue(patientId, out var actualPerType);

            expectedPerType ??= new SortedDictionary<string, int>(StringComparer.Ordinal);
            actualPerType ??= new SortedDictionary<string, int>(StringComparer.Ordinal);

            foreach (var type in expectedPerType.Keys.Union(actualPerType.Keys, StringComparer.Ordinal))
            {
                expectedPerType.TryGetValue(type, out var e);
                actualPerType.TryGetValue(type, out var a);
                if (e != a)
                    diffs.Add($"{label} patient='{patientId}', type='{type}' expected={e}, actual={a}");
            }
        }
    }

    private static string GetBaselinePath(string baselineName)
    {
        var configured = Environment.GetEnvironmentVariable(BaselineDirectoryEnvVar);
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.Combine(configured, $"{baselineName}.baseline.json");

        var root = FindRepositoryRoot() ?? AppContext.BaseDirectory;
        return Path.Combine(root, "Tests", "BackendE2ETests", "Baselines", $"{baselineName}.baseline.json");
    }

    private static string? FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;

            dir = dir.Parent;
        }

        return null;
    }

    private static async Task<ValidationBaselineDocument?> TryLoadBaselineAsync(string baselineName, string path)
    {
        if (File.Exists(path))
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<ValidationBaselineDocument>(json, JsonOptions);
        }

        var resourceName = $"LantanaGroup.Link.Tests.BackendE2ETests.Baselines.{baselineName}.baseline.json";
        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream == null)
            return null;

        using var reader = new StreamReader(stream);
        var jsonFromEmbedded = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<ValidationBaselineDocument>(jsonFromEmbedded, JsonOptions);
    }

    private static async Task WriteBaselineAsync(string path, ValidationBaselineDocument baseline)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(baseline, JsonOptions);
        await File.WriteAllTextAsync(path, json);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
