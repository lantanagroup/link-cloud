using System.Globalization;
using LantanaGroup.Link.PatientFlowSimulator.Generation;
using LantanaGroup.Link.PatientFlowSimulator.Planning;
using LantanaGroup.Link.PatientFlowSimulator.Scenario;

namespace LantanaGroup.Link.PatientFlowSimulator.Import;

/// <summary>
/// One scheduled run: make sure the current month has been generated, then upload every hourly file
/// whose hour has elapsed and that has no .done marker yet, oldest first. A failure stops the run so
/// later hours are never uploaded ahead of earlier ones; the next run picks up where this one stopped.
/// </summary>
public sealed class ImportRunner
{
    private readonly ImportOptions _options;
    private readonly Action<string> _log;

    public ImportRunner(ImportOptions options, Action<string> log)
    {
        _options = options;
        _log = log;
    }

    /// <summary>
    /// Runs once.
    /// </summary>
    public async Task<ImportResult> RunAsync(CancellationToken cancellationToken)
    {
        var result = new ImportResult();
        var scenario = ScenarioDefinition.Load(_options.ScenarioPath);

        if (_options.GenerateMissingMonths)
        {
            GenerateMissingMonths(scenario, result);
        }

        var due = FindDueFiles();
        if (due.Count == 0)
        {
            _log($"Nothing due at {_options.Now:u}.");
            return result;
        }

        _log($"{due.Count} file(s) due at {_options.Now:u}: {due[0].Hour:yyyy-MM-dd'T'HH'Z'} to {due[^1].Hour:yyyy-MM-dd'T'HH'Z'}.");

        if (_options.DryRun)
        {
            foreach (var file in due)
            {
                _log($"  would upload {file.Path}");
            }

            result.Imported = due.Count;
            return result;
        }

        using var client = new FhirImportClient(_options.FhirBaseUrl, _options.BearerToken, _options.BasicCredentials, _log);
        if (!await client.IsReachableAsync(cancellationToken))
        {
            result.FailedFile = due[0].Path;
            result.FailureReason = $"FHIR server at {_options.FhirBaseUrl} did not answer GET metadata.";
            result.Skipped = due.Count;
            _log(result.FailureReason);
            return result;
        }

        for (var i = 0; i < due.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = due[i];
            var json = await File.ReadAllTextAsync(file.Path, cancellationToken);
            var response = await client.PostTransactionAsync(json, cancellationToken);

            if (!response.Success)
            {
                result.FailedFile = file.Path;
                result.FailureReason = $"HTTP {response.StatusCode}: {response.Body}";
                result.Skipped = due.Count - i;
                _log($"FAILED {Path.GetFileName(file.Path)}: HTTP {response.StatusCode}");
                _log(response.Body);
                return result;
            }

            await WriteDoneMarkerAsync(file, response.StatusCode, cancellationToken);
            result.Imported++;
            _log($"  uploaded {Path.GetFileName(file.Path)} (HTTP {response.StatusCode})");
        }

        _log($"Uploaded {result.Imported} file(s).");
        return result;
    }

    // Each facility is in its own local month; whichever months that gives, make sure they exist.
    private void GenerateMissingMonths(ScenarioDefinition scenario, ImportResult result)
    {
        var months = scenario.Facilities
            .Select(f => YearMonth.Of(LocalTime.ToLocal(_options.Now, LocalTime.Resolve(f.TimeZone))))
            .Distinct()
            .OrderBy(m => m)
            .ToList();

        foreach (var month in months)
        {
            if (Directory.Exists(MonthWriter.MonthDirectory(_options.DataDirectory, month)))
            {
                continue;
            }

            _log($"Generating month {month}...");
            var output = new HourlyBundleBuilder(scenario).Build(month);
            var directory = MonthWriter.Write(output, _options.DataDirectory, overwrite: false);
            result.GeneratedMonths.Add(month.ToString());
            _log($"  wrote {output.Files.Count} file(s), {output.Manifest.EntryCount} resources to {directory}");
        }
    }

    private List<DueFile> FindDueFiles()
    {
        var due = new List<DueFile>();
        if (!Directory.Exists(_options.DataDirectory))
        {
            return due;
        }

        foreach (var monthDirectory in Directory.EnumerateDirectories(_options.DataDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(monthDirectory, "*.json"))
            {
                if (!MonthWriter.TryParseFileHour(path, out var hour))
                {
                    continue;
                }

                var file = new DueFile(hour, path);
                if (hour.AddHours(1) > _options.Now || File.Exists(file.DoneMarkerPath))
                {
                    continue;
                }

                due.Add(file);
            }
        }

        return due
            .OrderBy(f => f.Hour)
            .ThenBy(f => f.Path, StringComparer.Ordinal)
            .ToList();
    }

    private static Task WriteDoneMarkerAsync(DueFile file, int statusCode, CancellationToken cancellationToken)
    {
        var marker = $"{{ \"importedAtUtc\": \"{DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)}\", \"status\": {statusCode} }}";
        return File.WriteAllTextAsync(file.DoneMarkerPath, marker, cancellationToken);
    }
}
