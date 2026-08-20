using System.Text.Json;
using LantanaGroup.Automation.Helpers;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Validation;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class ReportAbsManifestValidatorTests
{
    private const string ExpectedStart = "2025-01-01T00:00:00Z";
    private const string ExpectedEnd = "2025-01-31T23:59:59Z";
    private const string DeviceProfile = "http://hl7.org/fhir/us/nhsn-dqm/StructureDefinition/nhsn-submitting-device";
    private const string PatientListProfile = "http://hl7.org/fhir/us/nhsn-dqm/StructureDefinition/poi-list";

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
    public async Task ValidateAllAsync_WithManifest_ExpectsOperationOutcomeWhenValidationWriterIsOn()
    {
        var output = new BufferingAutomationOutput();
        var patientId = "patient-oo-1";
        var measureId = "NHSNAcuteCareHospitalMonthlyInitialPopulation";
        var scheduleId = Guid.NewGuid();

        var internalAbsResources = BuildAbsResourcesWithExtraOperationOutcomes(
            patientId,
            measureId,
            ExpectedStart,
            ExpectedEnd);

        var manifest = new GenerationManifest
        {
            PatientIds = [patientId],
            Profiles =
            [
                new PatientProfile(new Dictionary<ProfiledMeasureType, MeasureEligibility>
                {
                    [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation] = MeasureEligibility.Qualifying
                })
            ],
            SelectedMeasures =
            [
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation
            ],
            ResourceKeysByPatient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                [patientId] = new(StringComparer.OrdinalIgnoreCase)
                {
                    $"Patient/{patientId}"
                }
            },
            ExpectedAbsPatientIdsOverride = new HashSet<string>(StringComparer.Ordinal) { patientId }
        };

        var reportClient = new Mock<IReportServiceClient>();
        reportClient
            .Setup(c => c.GetEntriesByScheduleAsync(scheduleId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<List<ReportEntryApiModel>>
            {
                StatusCode = 200,
                Body =
                [
                    new ReportEntryApiModel
                    {
                        Id = Guid.NewGuid(),
                        FacilityId = "Facility-UT",
                        PatientId = patientId,
                        ReportingStatus = ReportingStatus.FailedValidation,
                        SubmissionStatus = SubmissionStatus.Submitted,
                        MeasureReports =
                        [
                            new EntryMeasureReportApiModel
                            {
                                MeasureReportId = $"MR-{patientId}-1",
                                ReportType = measureId,
                                Status = EntryMeasureReportStatus.ReadyForValidation,
                                ResourceCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["Patient"] = 1,
                                    ["MeasureReport"] = 1,
                                }
                            }
                        ]
                    }
                ]
            });

        var validator = new ReportAbsManifestValidator(output, CreatePipelineDataReader(reportClient.Object));

        await validator.ValidateAllAsync(
            internalAbsResources,
            new[] { patientId },
            new[] { measureId },
            ExpectedStart,
            ExpectedEnd,
            facilityId: "Facility-UT",
            reportId: scheduleId.ToString(),
            generatedBundles: null,
            expectedManifestPatientListIds: new[] { patientId },
            expectDataAcquisitionData: true,
            manifest: manifest,
            operationOutcomeExpectations: new ReportAbsManifestValidator.OperationOutcomeExpectationSettings(
                ValidationWritesPreQualOperationOutcomeWhenInvalid: true));
    }

    [Fact]
    public async Task ValidateAllAsync_WhenTerminalReportabilityDiffers_AlignsMeasureReportExpectationToTerminalState()
    {
        var output = new BufferingAutomationOutput();
        var patientId = "Patient-UT-001";
        var scheduleId = Guid.NewGuid();
        var achMeasureId = "NHSNAcuteCareHospitalMonthlyInitialPopulation";
        var hypoMeasureId = "NHSNGlycemicControlHypoglycemicInitialPopulation";

        var internalAbsResources = BuildAbsResourcesForSingleReportableMeasure(patientId, achMeasureId, hypoMeasureId, ExpectedStart, ExpectedEnd);

        // Profile predicts Q for both ACH + Hypo, but terminal report state will expose
        // only one reportable measure row (ACH) and one NotReportable row (Hypo).
        var manifest = new GenerationManifest
        {
            PatientIds = [patientId],
            Profiles =
            [
                new PatientProfile(new Dictionary<ProfiledMeasureType, MeasureEligibility>
                {
                    [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation] = MeasureEligibility.Qualifying,
                    [ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation] = MeasureEligibility.Qualifying,
                })
            ],
            SelectedMeasures =
            [
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
                ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation,
            ],
            ResourceKeysByPatient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                [patientId] = new(StringComparer.OrdinalIgnoreCase)
                {
                    $"Patient/{patientId}",
                    $"Condition/{patientId}-Condition-001",
                }
            },
            ExpectedAbsPatientIdsOverride = new HashSet<string>(StringComparer.Ordinal) { patientId }
        };

        var reportClient = new Mock<IReportServiceClient>();
        reportClient
            .Setup(c => c.GetEntriesByScheduleAsync(scheduleId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<List<ReportEntryApiModel>>
            {
                StatusCode = 200,
                Body =
                [
                    new ReportEntryApiModel
                    {
                        Id = Guid.NewGuid(),
                        FacilityId = "Facility-UT",
                        PatientId = patientId,
                        ReportingStatus = ReportingStatus.PendingValidation,
                        SubmissionStatus = SubmissionStatus.Submitted,
                        MeasureReports =
                        [
                            new EntryMeasureReportApiModel
                            {
                                MeasureReportId = $"MR-{patientId}-ACH",
                                ReportType = achMeasureId,
                                Status = EntryMeasureReportStatus.ReadyForValidation,
                                ResourceCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["Patient"] = 1,
                                    ["Condition"] = 1,
                                    ["MeasureReport"] = 1,
                                }
                            },
                            new EntryMeasureReportApiModel
                            {
                                MeasureReportId = $"MR-{patientId}-HYPO",
                                ReportType = hypoMeasureId,
                                Status = EntryMeasureReportStatus.NotReportable,
                                ResourceCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                            }
                        ]
                    }
                ]
            });

        var validator = new ReportAbsManifestValidator(output, CreatePipelineDataReader(reportClient.Object));

        await validator.ValidateAllAsync(
            internalAbsResources,
            new[] { patientId },
            new[] { achMeasureId, hypoMeasureId },
            ExpectedStart,
            ExpectedEnd,
            facilityId: "Facility-UT",
            reportId: scheduleId.ToString(),
            generatedBundles: null,
            expectedManifestPatientListIds: new[] { patientId },
            expectDataAcquisitionData: true,
            manifest: manifest);

        Assert.Contains(output.Lines, line =>
            line.Contains("Aligning predicted reportable MeasureReport count to terminal state", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public async Task ValidateAllAsync_WhenDeviceIsMissingProfile_Fails()
    {
        var output = new BufferingAutomationOutput();
        var (patientIds, bundles) = FhirBundleGenerator.Generate(output, patientCount: 1, totalResourcesPerPatient: 120);
        var patientId = patientIds.Single();

        var mockExternalAbsResources = BuildMockExternalAbsResources(
            patientId, bundles, "MEASURE-UT-4", ExpectedStart, ExpectedEnd, deviceProfile: null);

        var validator = new ReportAbsManifestValidator(output, CreatePipelineDataReader());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            validator.ValidateAllAsync(
                mockExternalAbsResources,
                new[] { patientId },
                "MEASURE-UT-4",
                ExpectedStart,
                ExpectedEnd));

        Assert.Contains(output.Lines, line =>
            line.Contains("Manifest Device is missing meta.profile", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAllAsync_WhenPatientListProfileIsWrong_Fails()
    {
        var output = new BufferingAutomationOutput();
        var (patientIds, bundles) = FhirBundleGenerator.Generate(output, patientCount: 1, totalResourcesPerPatient: 120);
        var patientId = patientIds.Single();

        var mockExternalAbsResources = BuildMockExternalAbsResources(
            patientId,
            bundles,
            "MEASURE-UT-5",
            ExpectedStart,
            ExpectedEnd,
            patientListProfile: "https://www.cdc.gov/nhsn/nhsn-measures/StructureDefinition/poi-list");

        var validator = new ReportAbsManifestValidator(output, CreatePipelineDataReader());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            validator.ValidateAllAsync(
                mockExternalAbsResources,
                new[] { patientId },
                "MEASURE-UT-5",
                ExpectedStart,
                ExpectedEnd));

        Assert.Contains(output.Lines, line =>
            line.Contains("Manifest List meta.profile mismatch", StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, object> BuildMockExternalAbsResources(
        string patientId,
        IReadOnlyList<(string Name, string Json)> bundles,
        string measureId,
        string start,
        string end,
        string? deviceProfile = DeviceProfile,
        string? patientListProfile = PatientListProfile)
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
            SerializeWithOptionalProfile(deviceProfile, new Dictionary<string, object>
            {
                ["resourceType"] = "Device",
                ["id"] = deviceId
            }),
            SerializeWithOptionalProfile(patientListProfile, new Dictionary<string, object>
            {
                ["resourceType"] = "List",
                ["id"] = "PatientList-UT",
                ["status"] = "current",
                ["mode"] = "snapshot",
                ["extension"] = new object[]
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
                ["entry"] = new object[]
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

    private static Dictionary<string, object> BuildAbsResourcesWithExtraOperationOutcomes(
        string patientId,
        string measureId,
        string start,
        string end)
    {
        var manifestNdjson = string.Join("\n", new[]
        {
            JsonSerializer.Serialize(new { resourceType = "Organization", id = "Org-UT" }),
            JsonSerializer.Serialize(new { resourceType = "Device", id = "Device-UT", meta = new { profile = new[] { DeviceProfile } } }),
            JsonSerializer.Serialize(new
            {
                resourceType = "List",
                id = "PatientList-UT",
                meta = new { profile = new[] { PatientListProfile } },
                status = "current",
                mode = "snapshot",
                extension = new object[]
                {
                    new
                    {
                        url = "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/link-patient-list-applicable-period-extension",
                        valuePeriod = new { start, end }
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
                id = "Manifest-SubjectList-UT",
                type = "subject-list",
                measure = $"http://example/Measure/{measureId}|1.0.0",
                reporter = new { reference = "Organization/Org-UT" },
                period = new { start, end },
                contained = new object[]
                {
                    new
                    {
                        resourceType = "List",
                        id = "Contained-MeasureReport-List",
                        entry = new object[]
                        {
                            new { item = new { reference = $"MeasureReport/MR-{patientId}-1" } }
                        }
                    }
                }
            })
        });

        var patientNdjson = string.Join("\n", new[]
        {
            JsonSerializer.Serialize(new { resourceType = "Patient", id = patientId }),
            JsonSerializer.Serialize(new
            {
                resourceType = "MeasureReport",
                id = $"MR-{patientId}-1",
                type = "individual",
                subject = new { reference = $"Patient/{patientId}" }
            }),
            JsonSerializer.Serialize(new { resourceType = "OperationOutcome", id = $"oo-{patientId}-prequal" })
        });

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["manifest.ndjson"] = manifestNdjson,
            [$"patient-{patientId}.ndjson"] = patientNdjson
        };
    }

    /// <summary>
    /// Serializes a manifest resource, adding meta.profile only when a profile is supplied so
    /// tests can model a resource that is missing it entirely.
    /// </summary>
    private static string SerializeWithOptionalProfile(string? profile, Dictionary<string, object> resource)
    {
        if (profile != null)
            resource["meta"] = new { profile = new[] { profile } };

        return JsonSerializer.Serialize(resource);
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

    private static Dictionary<string, object> BuildAbsResourcesForSingleReportableMeasure(
        string patientId,
        string achMeasureId,
        string hypoMeasureId,
        string start,
        string end)
    {
        var manifestNdjson = string.Join("\n", new[]
        {
            JsonSerializer.Serialize(new { resourceType = "Organization", id = "Org-UT" }),
            JsonSerializer.Serialize(new { resourceType = "Device", id = "Device-UT", meta = new { profile = new[] { DeviceProfile } } }),
            JsonSerializer.Serialize(new
            {
                resourceType = "List",
                id = "PatientList-UT",
                meta = new { profile = new[] { PatientListProfile } },
                status = "current",
                mode = "snapshot",
                extension = new object[]
                {
                    new
                    {
                        url = "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/link-patient-list-applicable-period-extension",
                        valuePeriod = new { start, end }
                    }
                },
                entry = new object[] { new { item = new { reference = $"Patient/{patientId}" } } }
            }),
            JsonSerializer.Serialize(new
            {
                resourceType = "MeasureReport",
                id = "Manifest-Ach-UT",
                type = "subject-list",
                measure = $"http://example/Measure/{achMeasureId}|1.0.0",
                reporter = new { reference = "Organization/Org-UT" },
                period = new { start, end },
                contained = new object[]
                {
                    new
                    {
                        resourceType = "List",
                        id = "ach-list",
                        status = "current",
                        mode = "snapshot",
                        entry = new object[] { new { item = new { reference = $"MeasureReport/MR-{patientId}-ACH" } } }
                    }
                }
            }),
            JsonSerializer.Serialize(new
            {
                resourceType = "MeasureReport",
                id = "Manifest-Hypo-UT",
                type = "subject-list",
                measure = $"http://example/Measure/{hypoMeasureId}|1.0.0",
                reporter = new { reference = "Organization/Org-UT" },
                period = new { start, end },
                contained = new object[]
                {
                    new
                    {
                        resourceType = "List",
                        id = "hypo-list",
                        status = "current",
                        mode = "snapshot"
                    }
                }
            })
        });

        var patientNdjson = string.Join("\n", new[]
        {
            JsonSerializer.Serialize(new { resourceType = "Patient", id = patientId }),
            JsonSerializer.Serialize(new { resourceType = "Condition", id = $"{patientId}-Condition-001", subject = new { reference = $"Patient/{patientId}" } }),
            JsonSerializer.Serialize(new { resourceType = "MeasureReport", id = $"MR-{patientId}-ACH", type = "individual", subject = new { reference = $"Patient/{patientId}" } }),
        });

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["manifest.ndjson"] = manifestNdjson,
            [$"patient-{patientId}.ndjson"] = patientNdjson
        };
    }

    private static PipelineDataReader CreatePipelineDataReader(IReportServiceClient? reportClient = null)
    {
        return new PipelineDataReader(
            reportClient ?? new Mock<IReportServiceClient>().Object,
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
