using System.Text.Json;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.KafkaKeyProof.Tests;

public class KafkaKeyGoldenTests
{
    [Fact]
    public void FixtureMatchesCanonicalBytesAndMurmur2Partitions()
    {
        var root = FindRepoRoot();
        var path = GoldenFixturePath(root);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var mismatches = new List<string>();
        foreach (var row in document.RootElement.EnumerateArray())
        {
            var facilityId = row.GetProperty("facilityId").GetString();
            var patientElement = row.GetProperty("patientId");
            var patientId = patientElement.ValueKind == JsonValueKind.Null ? null : patientElement.GetString();
            var expectedKey = row.GetProperty("key").GetString();
            var actualKey = string.IsNullOrEmpty(patientId)
                ? KafkaKeys.ForFacility(facilityId)
                : KafkaKeys.ForPatient(facilityId, patientId);
            if (expectedKey != actualKey)
            {
                mismatches.Add(row.GetProperty("name").GetString() + " key expected " + expectedKey + " actual " + actualKey);
            }

            foreach (var partitionCount in new[] { 3, 6, 12, 24 })
            {
                var actualPartition = KafkaMurmur2.Partition(actualKey, partitionCount);
                var expectedPartition = row.GetProperty("partitions").GetProperty(partitionCount.ToString()).GetInt32();
                if (expectedPartition != actualPartition)
                {
                    mismatches.Add(row.GetProperty("name").GetString() + " p" + partitionCount + " expected " + expectedPartition + " actual " + actualPartition);
                }
            }
        }

        Assert.True(mismatches.Count == 0, string.Join("; ", mismatches));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "topics.txt")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }

    private static string GoldenFixturePath(string root)
    {
        var tests = ChildDirectory(root, "tests");
        var fixtures = ChildDirectory(tests, "fixtures");
        var match = Directory.EnumerateFiles(fixtures)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), "kafka-key-golden.json", StringComparison.OrdinalIgnoreCase));
        if (match == null)
        {
            throw new FileNotFoundException("Kafka key golden fixture was not found under the repository root.");
        }

        return match;
    }

    private static string ChildDirectory(string parent, string name)
    {
        var match = Directory.EnumerateDirectories(parent)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase));
        if (match == null)
        {
            throw new DirectoryNotFoundException("Could not find a " + name + " directory under " + parent + ".");
        }

        return match;
    }
}
