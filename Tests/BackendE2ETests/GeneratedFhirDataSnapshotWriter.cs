using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LantanaGroup.Link.Automation.Helpers;

namespace LantanaGroup.Link.Tests.E2ETests;

public static class GeneratedFhirDataSnapshotWriter
{
    public static string GetSnapshotDirectory(string testName)
    {
        var rootPath = Environment.GetEnvironmentVariable("E2E_GENERATED_FHIR_OUTPUT_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "generated-fhir-snapshots");

        return Path.Combine(rootPath, testName);
    }

    public static async Task WriteIfChangedAsync(
        IAutomationOutput output,
        string testName,
        int generationSeed,
        IReadOnlyCollection<string> patientIds,
        IReadOnlyList<(string Name, string Json)> bundles)
    {
        var testPath = GetSnapshotDirectory(testName);
        Directory.CreateDirectory(testPath);

        var orderedBundles = bundles
            .OrderBy(b => b.Name, StringComparer.Ordinal)
            .ToList();

        var currentHash = ComputeHash(orderedBundles);
        var hashFile = Path.Combine(testPath, "bundles.sha256");
        var previousHash = File.Exists(hashFile)
            ? (await File.ReadAllTextAsync(hashFile)).Trim()
            : null;

        if (string.Equals(previousHash, currentHash, StringComparison.OrdinalIgnoreCase))
        {
            output.WriteLine($"Generated FHIR snapshot unchanged for {testName}; skipping write ({testPath}).");
            return;
        }

        foreach (var file in Directory.EnumerateFiles(testPath, "*.json"))
        {
            File.Delete(file);
        }

        for (var i = 0; i < orderedBundles.Count; i++)
        {
            var bundle = orderedBundles[i];
            var safeName = SanitizeFileName(bundle.Name);
            var filename = Path.Combine(testPath, $"{i + 1:D4}-{safeName}.json");
            await File.WriteAllTextAsync(filename, bundle.Json);
        }

        var metadata = new
        {
            TestName = testName,
            GenerationSeed = generationSeed,
            PatientCount = patientIds.Count,
            PatientIds = patientIds,
            BundleCount = orderedBundles.Count,
            Hash = currentHash,
            GeneratedAtUtc = DateTime.UtcNow
        };

        await File.WriteAllTextAsync(
            Path.Combine(testPath, "metadata.json"),
            JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));

        await File.WriteAllTextAsync(hashFile, currentHash + Environment.NewLine);

        output.WriteLine($"Wrote generated FHIR snapshot for {testName}: {orderedBundles.Count} bundle(s) -> {testPath}");
    }

    private static string ComputeHash(IReadOnlyCollection<(string Name, string Json)> bundles)
    {
        using var sha = SHA256.Create();

        foreach (var bundle in bundles)
        {
            var nameBytes = Encoding.UTF8.GetBytes(bundle.Name);
            var jsonBytes = Encoding.UTF8.GetBytes(bundle.Json);

            sha.TransformBlock(nameBytes, 0, nameBytes.Length, null, 0);
            sha.TransformBlock([0x1E], 0, 1, null, 0); // record separator
            sha.TransformBlock(jsonBytes, 0, jsonBytes.Length, null, 0);
            sha.TransformBlock([0x1F], 0, 1, null, 0); // unit separator
        }

        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            sb.Append(invalid.Contains(c) ? '_' : c);
        }

        return sb.ToString();
    }
}
