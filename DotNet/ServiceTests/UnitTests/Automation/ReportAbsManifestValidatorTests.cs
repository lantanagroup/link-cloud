using System.Text.Json;
using LantanaGroup.Automation.Helpers;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Validation;
using LantanaGroup.Link.Sdk.Clients;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class ReportAbsManifestValidatorTests
{
    private const string ExpectedStart = "2025-01-01T00:00:00Z";
    private const string ExpectedEnd = "2025-01-31T23:59:59Z";

    [Fact]
    public async Task ValidateAllAsync_WithMockExternalAbsFromGeneratedData_Passes()
    {
        var output = new BufferingAutomationOutput();
        var (patientIds, bundles) = FhirBundleGenerator.Generate(output, patientCount: 1, totalResourcesPerPatient: 120);
        var patientId = patientIds.Single();

        var mockExternalAbsResources = BuildMockExternalAbsResources(patientId, bundles, "MEASURE-UT-1", ExpectedStart, ExpectedEnd);
        var validator = new ReportAbsManifestValidator(output, CreatePipelineDataReader());

        await validator.ValidateAllAsync(
            mockExternalAbsResources,
            new[] { patientId },
            "MEASURE-UT-1",
            ExpectedStart,
            ExpectedEnd);
    }

    [Fact]
    public async Task ValidateAllAsync_WithMissingPatientArtifact_Fails()
    {
        var output = new BufferingAutomationOutput();
        var (patientIds, bundles) = FhirBundleGenerator.Generate(output, patientCount: 1, totalResourcesPerPatient: 120);
        var patientId = patientIds.Single();

        var mockExternalAbsResources = BuildMockExternalAbsResources(patientId, bundles, "MEASURE-UT-2", ExpectedStart, ExpectedEnd);
        mockExternalAbsResources.Remove($"patient-{patientId}.ndjson");

        var validator = new ReportAbsManifestValidator(output, CreatePipelineDataReader());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            validator.ValidateAllAsync(
                mockExternalAbsResources,
                new[] { patientId },
                "MEASURE-UT-2",
                ExpectedStart,
                ExpectedEnd));

        Assert.Contains("VALIDATION failed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAllAsync_WithUnexpectedMeasureId_Fails()
    {
        var output = new BufferingAutomationOutput();
        var (patientIds, bundles) = FhirBundleGenerator.Generate(output, patientCount: 1, totalResourcesPerPatient: 120);
        var patientId = patientIds.Single();

        var mockExternalAbsResources = BuildMockExternalAbsResources(patientId, bundles, "MEASURE-UT-3", ExpectedStart, ExpectedEnd);
        var validator = new ReportAbsManifestValidator(output, CreatePipelineDataReader());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            validator.ValidateAllAsync(
                mockExternalAbsResources,
                new[] { patientId },
                "SOME-OTHER-MEASURE",
                ExpectedStart,
                ExpectedEnd));

        Assert.Contains("VALIDATION failed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, object> BuildMockExternalAbsResources(
        string patientId,
        IReadOnlyList<(string Name, string Json)> bundles,
        string measureId,
        string start,
        string end)
    {
        var generatedPatientJson = ExtractGeneratedPatientJson(patientId, bundles);

        var orgId = "Org-UT";
        var deviceId = "Device-UT";
        var manifestSubjectListMrId = "Manifest-SubjectList-UT";
        var patientMrId = $"MeasureReport-{patientId}";

        var manifestNdjson = string.Join("\n", new[]
        {
            JsonSerializer.Serialize(new
            {
                resourceType = "Organization",
                id = orgId
            }),
            JsonSerializer.Serialize(new
            {
                resourceType = "Device",
                id = deviceId
            }),
            JsonSerializer.Serialize(new
            {
                resourceType = "List",
                id = "PatientList-UT",
                status = "current",
                mode = "snapshot",
                extension = new object[]
                {
                    new
                    {
                        url = "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/link-patient-list-applicable-period-extension",
                        valuePeriod = new
                        {
                            start,
                            end
                        }
                    }
                },
                entry = new object[]
                {
                    new { item = new { reference = $"Patient/{patientId}" } }
                }
            }),
            JsonSerializer.Serialize(new
            {
                resourceType = "MeasureReport",
                id = manifestSubjectListMrId,
                type = "subject-list",
                measure = $"Measure/{measureId}",
                reporter = new { reference = $"Organization/{orgId}" },
                period = new { start, end },
                subject = new { reference = $"Patient/{patientId}" },
                contained = new object[]
                {
                    new
                    {
                        resourceType = "List",
                        id = "Contained-MeasureReport-List",
                        entry = new object[]
                        {
                            new { item = new { reference = $"MeasureReport/{patientMrId}" } }
                        }
                    }
                }
            })
        });

        var patientNdjson = string.Join("\n", new[]
        {
            generatedPatientJson,
            JsonSerializer.Serialize(new
            {
                resourceType = "MeasureReport",
                id = patientMrId,
                type = "individual",
                measure = $"Measure/{measureId}",
                subject = new { reference = $"Patient/{patientId}" },
                period = new { start, end },
                status = "complete"
            })
        });

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["manifest.ndjson"] = manifestNdjson,
            [$"patient-{patientId}.ndjson"] = patientNdjson
        };
    }

    private static string ExtractGeneratedPatientJson(string patientId, IReadOnlyList<(string Name, string Json)> bundles)
    {
        foreach (var (_, bundleJson) in bundles)
        {
            using var doc = JsonDocument.Parse(bundleJson);
            if (!doc.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("resource", out var resource) || resource.ValueKind != JsonValueKind.Object)
                    continue;

                var resourceType = resource.TryGetProperty("resourceType", out var rt) ? rt.GetString() : null;
                var id = resource.TryGetProperty("id", out var rid) ? rid.GetString() : null;

                if (string.Equals(resourceType, "Patient", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(id, patientId, StringComparison.Ordinal))
                {
                    return resource.GetRawText();
                }
            }
        }

        throw new InvalidOperationException($"Generated bundles did not include Patient/{patientId}.");
    }

    private static PipelineDataReader CreatePipelineDataReader()
    {
        return new PipelineDataReader(
            new Mock<IReportServiceClient>().Object,
            new Mock<IDataAcquisitionServiceClient>().Object,
            new Mock<INormalizationServiceClient>().Object,
            new Mock<IFacilityServiceClient>().Object);
    }

    private sealed class BufferingAutomationOutput : IAutomationOutput
    {
        public List<string> Lines { get; } = new();

        public void WriteLine(string message) => Lines.Add(message);

        public void WriteLine(string format, params object[] args) => Lines.Add(string.Format(format, args));
    }
}
