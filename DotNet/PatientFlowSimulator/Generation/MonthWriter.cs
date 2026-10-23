using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LantanaGroup.Link.PatientFlowSimulator.Planning;

namespace LantanaGroup.Link.PatientFlowSimulator.Generation;

/// <summary>
/// Lays a generated month out on disk as data/yyyy-MM/yyyy-MM-ddTHHZ.json files plus a manifest.
/// </summary>
public static class MonthWriter
{
    /// <summary>
    /// Name of the manifest written into every month directory.
    /// </summary>
    public const string ManifestFileName = "manifest.json";

    private const string HourFormat = "yyyy-MM-dd'T'HH'Z'";

    private static readonly JsonSerializerOptions ManifestOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Directory a month is written to.
    /// </summary>
    public static string MonthDirectory(string dataDirectory, YearMonth month)
        => Path.Combine(dataDirectory, month.ToString());

    /// <summary>
    /// File name of an hour, e.g. 2026-11-03T14Z.json.
    /// </summary>
    public static string FileName(DateTimeOffset hour)
        => hour.ToUniversalTime().ToString(HourFormat, CultureInfo.InvariantCulture) + ".json";

    /// <summary>
    /// Reads the hour back out of a file name produced by <see cref="FileName"/>.
    /// </summary>
    public static bool TryParseFileHour(string fileName, out DateTimeOffset hour)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var parsed = DateTimeOffset.TryParseExact(stem,
                                                  HourFormat,
                                                  CultureInfo.InvariantCulture,
                                                  DateTimeStyles.AssumeUniversal,
                                                  out hour);
        hour = hour.ToUniversalTime();
        return parsed;
    }

    /// <summary>
    /// Writes the month. An existing month directory is only replaced when <paramref name="overwrite"/> is set.
    /// </summary>
    public static string Write(MonthOutput output, string dataDirectory, bool overwrite)
    {
        var directory = MonthDirectory(dataDirectory, output.Month);
        if (Directory.Exists(directory))
        {
            if (!overwrite)
            {
                throw new InvalidOperationException(
                    $"Month {output.Month} already exists at '{directory}'. Use --force to regenerate it.");
            }

            foreach (var stale in Directory.EnumerateFiles(directory, "*.json").Concat(Directory.EnumerateFiles(directory, "*.done")))
            {
                File.Delete(stale);
            }
        }

        Directory.CreateDirectory(directory);

        foreach (var file in output.Files)
        {
            File.WriteAllText(Path.Combine(directory, file.FileName), FhirJson.Serialize(file.Bundle));
        }

        File.WriteAllText(Path.Combine(directory, ManifestFileName), JsonSerializer.Serialize(output.Manifest, ManifestOptions));

        return directory;
    }
}
