using System.Text.Json;
using Hl7.Fhir.Model;
using LantanaGroup.Automation.Generation.ResourceFactories;
using PatientProfile = LantanaGroup.Automation.Generation.PatientProfile;
using Microsoft.Extensions.DependencyInjection;
using Thetis.Generation.Abstractions;

namespace LantanaGroup.Automation.Generation.Thetis;

/// <summary>
/// Compiles a <see cref="PatientGenerationSpec"/> and executes Thetis Engine,
/// then attaches KD15 factory anchors (Device, CareTeam, CarePlan, census List).
/// Shared infra is not generated here (KD21).
/// </summary>
public sealed class ThetisPatientEntryGenerator : IPatientEntryGenerator
{
    public static ThetisPatientEntryGenerator Shared { get; } = new();

    public async Task<List<Bundle.EntryComponent>> GenerateAsync(
        PatientEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var patientSeed = request.BaseSeed + (request.Profile.SeedOffset ?? request.PatientIndex);
        var patientId = string.IsNullOrWhiteSpace(request.PatientId)
            ? request.Ids.PatientId(request.PatientIndex)
            : request.PatientId.Trim();
        var anchors = ScenarioResourceGeneration.ComputePatientAnchors(
            patientId, patientSeed, request.SharedPractitionerIds);

        DateTime encStart, encEnd;
        (encStart, encEnd) = FhirGenerationPipeline.DeriveEncounterWindowForProfile(
            request.Profile, patientSeed, request.ClinicalPeriodStart, request.ClinicalPeriodEnd);

        var spec = PatientSpecFactory.From(
            request.Profile, request.TotalResourcesPerPatient, request.Config);

        using var scope = ThetisEngineHost.Services.CreateScope();
        var compiler = scope.ServiceProvider.GetRequiredService<IPatientGraphCompiler>();
        var engine = scope.ServiceProvider.GetRequiredService<IThetisGenerationEngine>();

        var definition = compiler.Compile(spec);
        request.Output?.WriteLine(
            $"[Thetis] compiled dag nodes={definition.Nodes.Count} edges={definition.Edges.Count} " +
            $"class={spec.EncounterClass} obs={spec.ObservationCount} medReq={spec.MedicationRequestCount} " +
            $"hypo={spec.IncludeMedicationRequest}");

        var run = new GenerationRunRequest
        {
            RandomSeed = patientSeed,
            StartTime = encStart,
            EncounterEndTime = encEnd,
            PatientId = patientId,
            RunTag = request.Ids.RunTag,
            PatientOrdinal = request.PatientIndex,
            IdGenerator = new RunTagResourceIdGenerator(patientId),
            Parameters = new Dictionary<string, string>
            {
                ["encounterId"] = anchors.EncounterId,
                ["primaryConditionId"] = anchors.PrimaryDxId,
                ["organizationId"] = request.Ids.Organization,
                ["edLocationId"] = request.Ids.EdLocation,
                ["icuLocationId"] = request.Ids.IcuLocation,
                ["stepDownLocationId"] = request.Ids.StepDownLocation,
                ["attendingPractitionerId"] = anchors.AttendingPractId,
                ["admittingPractitionerId"] = anchors.AdmittingPractId,
                ["practitionerId"] = anchors.AttendingPractId,
                [PatientSpecFactory.LocationIdVar] = request.Ids.IcuLocation,
                [PatientSpecFactory.HypoInsulinMedicationIdVar] = request.Ids.HypoInsulinGlargineMedication,
                ["observationCount"] = spec.ObservationCount.ToString(),
                ["medicationRequestCount"] = spec.MedicationRequestCount.ToString(),
                ["medicationAdministrationCount"] = spec.MedicationAdministrationCount.ToString(),
                ["additionalConditionCount"] = spec.AdditionalConditionCount.ToString(),
                ["procedureCount"] = spec.ProcedureCount.ToString(),
                ["coverageCount"] = spec.CoverageCount.ToString(),
                ["serviceRequestCount"] = spec.ServiceRequestCount.ToString(),
                ["specimenCount"] = spec.SpecimenCount.ToString(),
                ["diagnosticReportCount"] = spec.DiagnosticReportCount.ToString()
            }
        };

        var result = await engine.ExecuteAsync(definition, run, cancellationToken);
        request.Output?.WriteLine(
            $"[Thetis] execute resources={result.ResourceCount} durationMs={result.DurationMs}");

        var entries = ExtractEntries(result.BundleJson);

        AppendKd15Anchors(entries, patientId, patientSeed, encStart, anchors, request.Ids);

        if (request.Profile.RequiresHypoglycemicMedication()
            && entries.All(e => e.Resource is not MedicationAdministration))
        {
            ScenarioResourceGeneration.AddHypoglycemicQualifyingMedicationEntries(
                entries, patientId, anchors.EncounterId, anchors.AttendingPractId,
                patientSeed, encStart, encEnd, request.Ids,
                request.ClinicalPeriodStart, request.ClinicalPeriodEnd);
        }

        AlignEncounterToSharedStay(entries, anchors, request.Ids, encStart, encEnd, request.Profile);

        var stamped = ScenarioResourceGeneration.ApplyGenerationRequirements(entries, request.RequirementsPlan);
        var stayLabel = request.Profile.RequiresInpatientEncounter() ? "ED/ICU/step-down" : "single-location";
        request.Output?.WriteLine(
            $"[Thetis] automation fixture overlay: stay locations={stayLabel}, " +
            $"generation-requirement applications={stamped}");

        return entries;
    }

    /// <summary>
    /// Overlay the Automation shared-stay graph onto the Thetis Encounter.
    /// Inpatient stays get ED → ICU → step-down scaled to [encStart, encEnd].
    /// AMB/EMER stays get a single ED location for the whole window so ICU/step-down
    /// periods cannot invert on short outpatient durations.
    /// </summary>
    private static void AlignEncounterToSharedStay(
        List<Bundle.EntryComponent> entries,
        ScenarioResourceGeneration.PatientAnchorContext anchors,
        FhirBundleGenerator.SharedIds ids,
        DateTime encStart,
        DateTime encEnd,
        PatientProfile profile)
    {
        var encounter = entries.Select(e => e.Resource).OfType<Encounter>().FirstOrDefault();
        if (encounter is null)
            return;

        if (encEnd <= encStart)
            encEnd = encStart.AddMinutes(1);

        encounter.Location = profile.RequiresInpatientEncounter()
            ? BuildInpatientStayLocations(ids, encStart, encEnd)
            : BuildSingleStayLocation(ids, encStart, encEnd);

        if (encounter.ServiceProvider is null || string.IsNullOrWhiteSpace(encounter.ServiceProvider.Reference))
        {
            encounter.ServiceProvider = new ResourceReference($"Organization/{ids.Organization}")
            {
                Display = "General Test Hospital"
            };
        }

        if (encounter.Participant is not { Count: > 0 })
        {
            encounter.Participant =
            [
                new Encounter.ParticipantComponent
                {
                    Type =
                    [
                        new CodeableConcept
                        {
                            Coding =
                            [
                                new Coding("http://terminology.hl7.org/CodeSystem/v3-ParticipationType", "ATND", "attender")
                            ],
                            Text = "Attending"
                        }
                    ],
                    Period = encounter.Period,
                    Individual = new ResourceReference($"Practitioner/{anchors.AttendingPractId}")
                    {
                        Display = "Attending Physician"
                    }
                },
                new Encounter.ParticipantComponent
                {
                    Type =
                    [
                        new CodeableConcept
                        {
                            Coding =
                            [
                                new Coding("http://terminology.hl7.org/CodeSystem/v3-ParticipationType", "ADM", "admitter")
                            ],
                            Text = "Admitting"
                        }
                    ],
                    Period = new Period
                    {
                        StartElement = encounter.Period?.StartElement,
                        EndElement = new FhirDateTime(Min(encStart.AddHours(2), encEnd))
                    },
                    Individual = new ResourceReference($"Practitioner/{anchors.AdmittingPractId}")
                    {
                        Display = "Admitting Physician"
                    }
                }
            ];
        }
    }

    private static List<Encounter.LocationComponent> BuildSingleStayLocation(
        FhirBundleGenerator.SharedIds ids,
        DateTime encStart,
        DateTime encEnd) =>
    [
        new Encounter.LocationComponent
        {
            Location = new ResourceReference($"Location/{ids.EdLocation}") { Display = "Emergency Department" },
            Status = Encounter.EncounterLocationStatus.Completed,
            Period = Period(encStart, encEnd)
        }
    ];

    private static List<Encounter.LocationComponent> BuildInpatientStayLocations(
        FhirBundleGenerator.SharedIds ids,
        DateTime encStart,
        DateTime encEnd)
    {
        var total = encEnd - encStart;
        var third = TimeSpan.FromTicks(Math.Max(1, total.Ticks / 3));
        var edLen = Min(TimeSpan.FromHours(4), third);
        var stepLen = Min(TimeSpan.FromDays(1), third);
        if (edLen + stepLen >= total)
        {
            edLen = third;
            stepLen = third;
        }

        var edEnd = encStart + edLen;
        var stepStart = encEnd - stepLen;
        if (stepStart < edEnd)
            stepStart = edEnd;

        return
        [
            new Encounter.LocationComponent
            {
                Location = new ResourceReference($"Location/{ids.EdLocation}") { Display = "Emergency Department" },
                Status = Encounter.EncounterLocationStatus.Completed,
                Period = Period(encStart, edEnd)
            },
            new Encounter.LocationComponent
            {
                Location = new ResourceReference($"Location/{ids.IcuLocation}") { Display = "Intensive Care Unit" },
                Status = Encounter.EncounterLocationStatus.Completed,
                Period = Period(edEnd, stepStart)
            },
            new Encounter.LocationComponent
            {
                Location = new ResourceReference($"Location/{ids.StepDownLocation}") { Display = "Step-Down Unit" },
                Status = Encounter.EncounterLocationStatus.Completed,
                Period = Period(stepStart, encEnd)
            }
        ];
    }

    private static Period Period(DateTime start, DateTime end) => new()
    {
        StartElement = new FhirDateTime(start),
        EndElement = new FhirDateTime(end < start ? start : end)
    };

    private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a <= b ? a : b;

    private static List<Bundle.EntryComponent> ExtractEntries(string bundleJson)
    {
        var entries = new List<Bundle.EntryComponent>();
        using var doc = JsonDocument.Parse(bundleJson);
        if (!doc.RootElement.TryGetProperty("entry", out var entryArray)
            || entryArray.ValueKind != JsonValueKind.Array)
        {
            return entries;
        }

        var options = FhirSerializerOptions.ForFhirWithoutValidation();
        foreach (var entry in entryArray.EnumerateArray())
        {
            if (!entry.TryGetProperty("resource", out var resourceEl))
                continue;

            var resource = JsonSerializer.Deserialize<Resource>(resourceEl.GetRawText(), options);
            if (resource?.Id is null)
                continue;

            entries.Add(ScenarioResourceGeneration.Entry($"{resource.TypeName}/{resource.Id}", resource));
        }

        return entries;
    }

    private static void AppendKd15Anchors(
        List<Bundle.EntryComponent> entries,
        string patientId,
        int patientSeed,
        DateTime encStart,
        ScenarioResourceGeneration.PatientAnchorContext anchors,
        FhirBundleGenerator.SharedIds ids)
    {
        if (!HasType(entries, "Device"))
        {
            entries.Add(ScenarioResourceGeneration.Entry($"Device/{anchors.PatientDeviceId}",
                DeviceFactory.Generate(anchors.PatientDeviceId, patientSeed, patientId)));
        }

        if (!HasType(entries, "CareTeam"))
        {
            entries.Add(ScenarioResourceGeneration.Entry($"CareTeam/{anchors.CareTeamId}",
                CareTeamFactory.Generate(anchors.CareTeamId, patientId, anchors.EncounterId,
                    anchors.AttendingPractId, encStart, ids.Organization)));
        }

        if (!HasType(entries, "CarePlan"))
        {
            entries.Add(ScenarioResourceGeneration.Entry($"CarePlan/{anchors.CarePlanId}",
                CarePlanFactory.Generate(anchors.CarePlanId, patientId, anchors.EncounterId,
                    anchors.CareTeamId, encStart, patientSeed)));
        }

        if (!HasType(entries, "List"))
        {
            var listId = $"SyntheticList-{patientId}";
            entries.Add(ScenarioResourceGeneration.Entry($"List/{listId}",
                CensusListFactory.Generate(listId, patientId, encStart)));
        }
    }

    private static bool HasType(List<Bundle.EntryComponent> entries, string type) =>
        entries.Any(e => string.Equals(e.Resource?.TypeName, type, StringComparison.OrdinalIgnoreCase));
}
