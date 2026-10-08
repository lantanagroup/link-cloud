using System.Text.RegularExpressions;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.KafkaKeyProof.Tests;

public class KafkaKeyLegacyTests
{
    [Fact]
    public void ReadsLegacyResourceKey()
    {
        const string key = "{\"facilityId\":\"fac\",\"patientId\":\"pat\"}";
        Assert.True(KafkaKeyLegacy.TryReadFacility(key, out var facilityId));
        Assert.Equal("fac", facilityId);
        Assert.True(KafkaKeyLegacy.TryReadPatient(key, out var patientId));
        Assert.Equal("pat", patientId);
    }

    [Fact]
    public void ReadsLegacyReportKeyIgnoringCase()
    {
        var reportId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var key = "{\"FacilityId\":\"fac\",\"ReportScheduleId\":\"" + reportId + "\"}";
        Assert.True(KafkaKeyLegacy.TryReadReportScheduleId(key, out var parsed));
        Assert.Equal(reportId, parsed);
        Assert.Equal("fac", KafkaIdentity.Facility(null, key));
        Assert.Equal(reportId, KafkaIdentity.ReportSchedule(null, key));
    }

    [Fact]
    public void PlainFacilityKeyIsLegacy()
    {
        Assert.Equal("fac", KafkaIdentity.RequireFacility(null, "fac"));
        Assert.Equal("fac", KafkaIdentity.RequireFacility(null, "{\"facilityId\":\"fac\",\"patientId\":\"pat\"}"));
        Assert.Equal("pat", KafkaIdentity.RequirePatient(null, "{\"facilityId\":\"fac\",\"patientId\":\"pat\"}"));
    }

    [Fact]
    public void ValueWinsOverLegacyKey()
    {
        Assert.Equal("from-value", KafkaIdentity.Facility("from-value", "{\"facilityId\":\"from-key\"}"));
    }

    [Fact]
    public void ServiceNameKeyIsNotAFacility()
    {
        Assert.False(KafkaKeyLegacy.TryReadFacility("Audit", out _));
        Assert.False(KafkaKeyLegacy.TryReadFacility("  QueryDispatch  ", out _));
        Assert.Null(KafkaIdentity.Facility(null, "DataAcquisitionWorker"));
        Assert.False(KafkaKeyLegacy.TryReadFacility("measureeval", out _));
        Assert.False(KafkaKeyLegacy.TryReadFacility("ValidationService", out _));
        Assert.Equal("facility-1", KafkaIdentity.Facility(null, "facility-1"));
        Assert.True(KafkaKeyLegacy.TryReadFacility("{\"facilityId\":\"Audit\"}", out var fromJson));
        Assert.Equal("Audit", fromJson);
    }

    [Fact]
    public void EveryServiceNameConstantIsNotAFacilityKey()
    {
        var root = FindRepoRoot();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "DotNet"), "*.cs", SearchOption.AllDirectories))
        {
            if (IsSkipped(file))
            {
                continue;
            }

            foreach (Match match in Regex.Matches(File.ReadAllText(file), "const string ServiceName = \"([^\"]+)\""))
            {
                names.Add(match.Groups[1].Value);
            }
        }

        Assert.NotEmpty(names);
        foreach (var name in names)
        {
            Assert.False(KafkaKeyLegacy.TryReadFacility(name, out _), name);
        }
    }

    [Fact]
    public void InvalidKeyDoesNotHideValueIds()
    {
        Assert.Equal("from-value", KafkaIdentity.Facility("from-value", "not-json-and-not-a-facility-we-need"));
        Assert.Null(KafkaIdentity.Patient(null, "{\"facilityId\":\"fac\"}"));
    }

    private static bool IsSkipped(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => part is "bin" or "obj");
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
}
