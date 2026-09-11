using LantanaGroup.Automation.Generation;
using LantanaGroup.Automation.Generation.ResourceFactories;
using LantanaGroup.Automation.Helpers;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using System.Globalization;
using System.Text.Json;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class QueryPlanAcquisitionSimulatorTests
{
    private const string PeriodStart = "2024-01-01T00:00:00Z";
    private const string PeriodEnd = "2024-12-31T23:59:59Z";

    /// <summary>
    /// Captures simulator warnings so we can assert fail-closed behaviour emitted a warning.
    /// </summary>
    private sealed class CapturingOutput : IAutomationOutput
    {
        public List<string> Lines { get; } = new();
        public void WriteLine(string message) => Lines.Add(message);
        public void WriteLine(string format, params object[] args) => Lines.Add(string.Format(format, args));
    }

    private static (string ResourceType, string ResourceId, string Key, JsonElement Resource) Entry(
        string type, string id, string json)
    {
        var doc = JsonDocument.Parse(json);
        return (type, id, $"{type}/{id}", doc.RootElement.Clone());
    }

    private static QueryPlanInput PlanWithEncounterAndObservation() =>
        new()
        {
            EhrDescription = "Test",
            LookBack = "P0D",
            InitialQueries =
            [
                new QueryPlanQueryEntry
                {
                    ResourceType = "Encounter",
                    QueryConfigType = "Parameter",
                    Parameters =
                    [
                        new QueryPlanParameterEntry { ParameterType = "Variable", Name = "patient", Variable = 0 },
                        new QueryPlanParameterEntry { ParameterType = "Variable", Name = "date", Variable = 2, Format = "ge{0}" },
                        new QueryPlanParameterEntry { ParameterType = "Variable", Name = "date", Variable = 3, Format = "le{0}" }
                    ]
                }
            ],
            SupplementalQueries =
            [
                new QueryPlanQueryEntry
                {
                    ResourceType = "Observation",
                    QueryConfigType = "Parameter",
                    Parameters =
                    [
                        new QueryPlanParameterEntry { ParameterType = "Variable", Name = "patient", Variable = 0 },
                        new QueryPlanParameterEntry { ParameterType = "Variable", Name = "date", Variable = 2, Format = "ge{0}" },
                        new QueryPlanParameterEntry { ParameterType = "Variable", Name = "date", Variable = 3, Format = "le{0}" }
                    ]
                },
                new QueryPlanQueryEntry
                {
                    ResourceType = "Procedure",
                    QueryConfigType = "Parameter",
                    Parameters =
                    [
                        new QueryPlanParameterEntry { ParameterType = "Variable", Name = "patient", Variable = 0 },
                        new QueryPlanParameterEntry { ParameterType = "Variable", Name = "date", Variable = 2, Format = "ge{0}" },
                        new QueryPlanParameterEntry { ParameterType = "Variable", Name = "date", Variable = 3, Format = "le{0}" }
                    ]
                }
            ]
        };

    // ---------- Encounter period overlap ----------

    [Fact]
    public void Encounter_StartedBeforePeriod_ButEndedInside_IsAcquired()
    {
        // Production: FHIR `date` search on Encounter uses period overlap. An encounter
        // with period.start=2023-12-30 and period.end=2024-01-05 OVERLAPS [2024-01-01, ...].
        var entry = Entry("Encounter", "E1", """
            { "resourceType":"Encounter","id":"E1",
              "period": { "start":"2023-12-30T08:00:00Z", "end":"2024-01-05T08:00:00Z" } }
            """);

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1", new[] { entry }, null, PlanWithEncounterAndObservation(), PeriodStart, PeriodEnd);

        Assert.Contains("Encounter/E1", acquired);
    }

    [Fact]
    public void Encounter_StartedInside_ButEndedAfterPeriod_IsAcquired()
    {
        var entry = Entry("Encounter", "E2", """
            { "resourceType":"Encounter","id":"E2",
              "period": { "start":"2024-12-30T08:00:00Z", "end":"2025-01-03T08:00:00Z" } }
            """);

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1", new[] { entry }, null, PlanWithEncounterAndObservation(), PeriodStart, PeriodEnd);

        Assert.Contains("Encounter/E2", acquired);
    }

    [Fact]
    public void Encounter_EntirelyBeforePeriod_IsExcluded()
    {
        var entry = Entry("Encounter", "E3", """
            { "resourceType":"Encounter","id":"E3",
              "period": { "start":"2023-06-01T08:00:00Z", "end":"2023-06-05T08:00:00Z" } }
            """);

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1", new[] { entry }, null, PlanWithEncounterAndObservation(), PeriodStart, PeriodEnd);

        Assert.DoesNotContain("Encounter/E3", acquired);
    }

    [Fact]
    public void Encounter_EntirelyAfterPeriod_IsExcluded()
    {
        var entry = Entry("Encounter", "E4", """
            { "resourceType":"Encounter","id":"E4",
              "period": { "start":"2025-06-01T08:00:00Z", "end":"2025-06-05T08:00:00Z" } }
            """);

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1", new[] { entry }, null, PlanWithEncounterAndObservation(), PeriodStart, PeriodEnd);

        Assert.DoesNotContain("Encounter/E4", acquired);
    }

    [Fact]
    public void Encounter_OpenEndedPeriod_StartedInside_IsAcquired()
    {
        // FHIR allows period.end to be omitted (still in progress). Such encounters extend
        // to +inf and overlap any period whose end is >= period.start.
        var entry = Entry("Encounter", "E5", """
            { "resourceType":"Encounter","id":"E5",
              "period": { "start":"2024-06-01T08:00:00Z" } }
            """);

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1", new[] { entry }, null, PlanWithEncounterAndObservation(), PeriodStart, PeriodEnd);

        Assert.Contains("Encounter/E5", acquired);
    }

    // ---------- Observation effectivePeriod ----------

    [Fact]
    public void Observation_EffectivePeriod_OverlapsPeriod_IsAcquired()
    {
        // Imported/EHR lab observations may use effectivePeriod rather than
        // effectiveDateTime. The simulator must still apply FHIR overlap.
        var entry = Entry("Observation", "O1", """
            { "resourceType":"Observation","id":"O1",
              "category":[{"coding":[{"code":"laboratory"}]}],
              "effectivePeriod": { "start":"2024-03-15T08:00:00Z", "end":"2024-03-15T09:00:00Z" } }
            """);

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1", new[] { entry }, null, PlanWithEncounterAndObservation(), PeriodStart, PeriodEnd);

        Assert.Contains("Observation/O1", acquired);
    }

    [Fact]
    public void Observation_EffectivePeriod_OutsidePeriod_IsExcluded()
    {
        var entry = Entry("Observation", "O2", """
            { "resourceType":"Observation","id":"O2",
              "category":[{"coding":[{"code":"laboratory"}]}],
              "effectivePeriod": { "start":"2020-03-15T08:00:00Z", "end":"2020-03-15T09:00:00Z" } }
            """);

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1", new[] { entry }, null, PlanWithEncounterAndObservation(), PeriodStart, PeriodEnd);

        Assert.DoesNotContain("Observation/O2", acquired);
    }

    [Fact]
    public void Observation_EffectiveDateTime_StillWorks()
    {
        var entry = Entry("Observation", "O3", """
            { "resourceType":"Observation","id":"O3",
              "category":[{"coding":[{"code":"vital-signs"}]}],
              "effectiveDateTime":"2024-06-01T12:00:00Z" }
            """);

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1", new[] { entry }, null, PlanWithEncounterAndObservation(), PeriodStart, PeriodEnd);

        Assert.Contains("Observation/O3", acquired);
    }

    [Fact]
    public void Observation_EffectiveInstant_StillWorks()
    {
        var entry = Entry("Observation", "O4", """
            { "resourceType":"Observation","id":"O4",
              "category":[{"coding":[{"code":"vital-signs"}]}],
              "effectiveInstant":"2024-06-01T12:00:00Z" }
            """);

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1", new[] { entry }, null, PlanWithEncounterAndObservation(), PeriodStart, PeriodEnd);

        Assert.Contains("Observation/O4", acquired);
    }

    [Fact]
    public void Observation_EffectiveTiming_EventRange_OverlapsPeriod_IsAcquired()
    {
        var entry = Entry("Observation", "O5", """
            { "resourceType":"Observation","id":"O5",
              "category":[{"coding":[{"code":"laboratory"}]}],
              "effectiveTiming": {
                "event": ["2024-03-15T08:00:00Z", "2024-03-15T09:00:00Z"]
              } }
            """);

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1", new[] { entry }, null, PlanWithEncounterAndObservation(), PeriodStart, PeriodEnd);

        Assert.Contains("Observation/O5", acquired);
    }

    [Fact]
    public void Observation_EffectiveTiming_BoundsPeriod_OverlapsPeriod_IsAcquired()
    {
        var entry = Entry("Observation", "O6", """
            { "resourceType":"Observation","id":"O6",
              "category":[{"coding":[{"code":"laboratory"}]}],
              "effectiveTiming": {
                "repeat": {
                  "boundsPeriod": { "start":"2024-04-01T08:00:00Z", "end":"2024-04-01T09:00:00Z" }
                }
              } }
            """);

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1", new[] { entry }, null, PlanWithEncounterAndObservation(), PeriodStart, PeriodEnd);

        Assert.Contains("Observation/O6", acquired);
    }

    [Fact]
    public void Observation_EffectiveTiming_NoUsableDates_IsExcludedAndWarns()
    {
        var entry = Entry("Observation", "O-TimingNoDate", """
            { "resourceType":"Observation","id":"O-TimingNoDate",
              "category":[{"coding":[{"code":"laboratory"}]}],
              "effectiveTiming": {
                "event": ["not-a-date"],
                "repeat": { "boundsPeriod": { } }
              } }
            """);
        var output = new CapturingOutput();

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1", new[] { entry }, null, PlanWithEncounterAndObservation(), PeriodStart, PeriodEnd, output);

        Assert.DoesNotContain("Observation/O-TimingNoDate", acquired);
        Assert.Contains(output.Lines, l => l.Contains("Observation/O-TimingNoDate") && l.Contains("fail-closed"));
    }

    // ---------- Procedure performedPeriod ----------

    [Fact]
    public void Procedure_PerformedPeriod_OverlapsPeriod_IsAcquired()
    {
        var entry = Entry("Procedure", "P1", """
            { "resourceType":"Procedure","id":"P1",
              "performedPeriod": { "start":"2024-04-01T10:00:00Z", "end":"2024-04-01T11:00:00Z" } }
            """);

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1", new[] { entry }, null, PlanWithEncounterAndObservation(), PeriodStart, PeriodEnd);

        Assert.Contains("Procedure/P1", acquired);
    }

    [Fact]
    public void Procedure_PerformedPeriod_OutsidePeriod_IsExcluded()
    {
        var entry = Entry("Procedure", "P2", """
            { "resourceType":"Procedure","id":"P2",
              "performedPeriod": { "start":"2026-04-01T10:00:00Z", "end":"2026-04-01T11:00:00Z" } }
            """);

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1", new[] { entry }, null, PlanWithEncounterAndObservation(), PeriodStart, PeriodEnd);

        Assert.DoesNotContain("Procedure/P2", acquired);
    }

    // ---------- Fail-closed on unrecognized shapes (G8) ----------

    [Fact]
    public void Observation_NoRecognizedDateField_IsExcludedAndWarns()
    {
        // No effective[x], no issued — date filter cannot be honestly applied.
        var entry = Entry("Observation", "O-NoDate", """
            { "resourceType":"Observation","id":"O-NoDate",
              "category":[{"coding":[{"code":"laboratory"}]}] }
            """);
        var output = new CapturingOutput();

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1", new[] { entry }, null, PlanWithEncounterAndObservation(), PeriodStart, PeriodEnd, output);

        Assert.DoesNotContain("Observation/O-NoDate", acquired);
        Assert.Contains(output.Lines, l => l.Contains("Observation/O-NoDate") && l.Contains("fail-closed"));
    }

    [Fact]
    public void UnrecognizedDateShape_WarnsOnlyOncePerResource()
    {
        // Both ge and le date params iterate per resource; warning should be emitted once.
        var entry = Entry("Observation", "O-NoDate2", """
            { "resourceType":"Observation","id":"O-NoDate2",
              "category":[{"coding":[{"code":"laboratory"}]}] }
            """);
        var output = new CapturingOutput();

        QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1", new[] { entry }, null, PlanWithEncounterAndObservation(), PeriodStart, PeriodEnd, output);

        var matches = output.Lines.Count(l => l.Contains("Observation/O-NoDate2"));
        Assert.Equal(1, matches);
    }

    // ---------- No-period-supplied: behavior unchanged ----------

    [Fact]
    public void NoClinicalPeriod_AllResourcesAreAcquired()
    {
        var encounter = Entry("Encounter", "E-Any", """
            { "resourceType":"Encounter","id":"E-Any",
              "period": { "start":"2099-01-01T00:00:00Z" } }
            """);
        var observation = Entry("Observation", "O-NoDate3", """
            { "resourceType":"Observation","id":"O-NoDate3",
              "category":[{"coding":[{"code":"laboratory"}]}] }
            """);

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1", new[] { encounter, observation }, null, PlanWithEncounterAndObservation(),
            clinicalPeriodStart: null, clinicalPeriodEnd: null);

        Assert.Contains("Encounter/E-Any", acquired);
        // Even fail-closed only kicks in when the date filter has actual bounds. With no
        // bounds the date check is vacuous; resource is included.
        Assert.Contains("Observation/O-NoDate3", acquired);
    }

    [Fact]
    public void Location_ReferenceQuery_FollowsPartOfParent()
    {
        var encounter = Entry("Encounter", "E1", """
            { "resourceType":"Encounter","id":"E1",
              "period": { "start":"2024-06-01T08:00:00Z", "end":"2024-06-05T08:00:00Z" },
              "location":[{"location":{"reference":"Location/L-ROOM"}}] }
            """);
        var room = Entry("Location", "L-ROOM", """
            { "resourceType":"Location","id":"L-ROOM",
              "partOf":{"reference":"Location/L-HOSP"} }
            """);
        var hospital = Entry("Location", "L-HOSP", """
            { "resourceType":"Location","id":"L-HOSP" }
            """);
        var unused = Entry("Location", "L-UNUSED", """
            { "resourceType":"Location","id":"L-UNUSED",
              "partOf":{"reference":"Location/L-HOSP"} }
            """);

        var plan = new QueryPlanInput
        {
            EhrDescription = "Test",
            LookBack = "P0D",
            InitialQueries =
            [
                new QueryPlanQueryEntry
                {
                    ResourceType = "Encounter",
                    QueryConfigType = "Parameter",
                    Parameters =
                    [
                        new QueryPlanParameterEntry { ParameterType = "Variable", Name = "patient", Variable = 0 },
                        new QueryPlanParameterEntry { ParameterType = "Variable", Name = "date", Variable = 2, Format = "ge{0}" },
                        new QueryPlanParameterEntry { ParameterType = "Variable", Name = "date", Variable = 3, Format = "le{0}" }
                    ]
                },
                new QueryPlanQueryEntry
                {
                    ResourceType = "Location",
                    QueryConfigType = "Reference",
                    OperationType = 1,
                    Paged = 100
                }
            ]
        };

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1",
            [encounter],
            [room, hospital, unused],
            plan,
            PeriodStart,
            PeriodEnd);

        Assert.Contains("Encounter/E1", acquired);
        Assert.Contains("Location/L-ROOM", acquired);
        Assert.Contains("Location/L-HOSP", acquired);
        Assert.DoesNotContain("Location/L-UNUSED", acquired);
    }

    [Fact]
    public void DiagnosticReport_IssuedInPeriod_EffectiveBeforePeriod_IsExcluded()
    {
        var encounter = Entry("Encounter", "E1", """
            { "resourceType":"Encounter","id":"E1",
              "period": { "start":"2024-06-01T08:00:00Z", "end":"2024-06-05T08:00:00Z" } }
            """);
        var report = Entry("DiagnosticReport", "DxRpt-042", """
            { "resourceType":"DiagnosticReport","id":"DxRpt-042",
              "encounter":{"reference":"Encounter/E1"},
              "effectiveDateTime":"2023-12-01T08:00:00Z",
              "issued":"2024-06-15T08:00:00Z" }
            """);

        var plan = new QueryPlanInput
        {
            EhrDescription = "Test",
            LookBack = "P0D",
            InitialQueries =
            [
                new QueryPlanQueryEntry
                {
                    ResourceType = "Encounter",
                    QueryConfigType = "Parameter",
                    Parameters =
                    [
                        new QueryPlanParameterEntry { ParameterType = "Variable", Name = "patient", Variable = 0 },
                        new QueryPlanParameterEntry { ParameterType = "Variable", Name = "date", Variable = 2, Format = "ge{0}" },
                        new QueryPlanParameterEntry { ParameterType = "Variable", Name = "date", Variable = 3, Format = "le{0}" }
                    ]
                }
            ],
            SupplementalQueries =
            [
                new QueryPlanQueryEntry
                {
                    ResourceType = "DiagnosticReport",
                    QueryConfigType = "Parameter",
                    Parameters =
                    [
                        new QueryPlanParameterEntry { ParameterType = "Variable", Name = "patient", Variable = 0 },
                        new QueryPlanParameterEntry { ParameterType = "Variable", Name = "date", Variable = 2, Format = "ge{0}" },
                        new QueryPlanParameterEntry { ParameterType = "Variable", Name = "date", Variable = 3, Format = "le{0}" }
                    ]
                }
            ]
        };

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1",
            [encounter, report],
            null,
            plan,
            PeriodStart,
            PeriodEnd,
            allowEncounterAnchoredDateOverrideForOutOfRange: false);

        Assert.Contains("Encounter/E1", acquired);
        Assert.DoesNotContain("DiagnosticReport/DxRpt-042", acquired);
    }

    [Fact]
    public void DailyWindow_FhirBundleGeneratorObservations_SimulatorMatchesPeriodOverlap()
    {
        var mpStart = new DateTimeOffset(2023, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var mpEnd = new DateTimeOffset(2023, 1, 15, 23, 59, 59, TimeSpan.Zero);
        var encStart = mpStart.AddHours(-6).UtcDateTime;
        var encEnd = mpEnd.AddHours(6).UtcDateTime;

        var serializerOptions = new JsonSerializerOptions();
        serializerOptions.ForFhir(ModelInfo.ModelInspector);

        var encounterJson = JsonSerializer.Serialize(
            new Encounter
            {
                Id = "E1",
                Period = new Period
                {
                    StartElement = new FhirDateTime(encStart),
                    EndElement = new FhirDateTime(encEnd)
                }
            },
            serializerOptions);

        var entries = new List<(string ResourceType, string ResourceId, string Key, JsonElement Resource)>
        {
            Entry("Encounter", "E1", encounterJson)
        };

        var observationIds = new List<string>();
        var specimenIds = new List<string> { "S1" };
        const int observationCount = 80;
        for (var i = 0; i < observationCount; i++)
        {
            var offset = TimeSpan.FromMinutes((double)i / observationCount * (encEnd - encStart).TotalMinutes);
            var effective = encStart.Add(offset);
            var id = $"O-{i:D3}";
            var obs = ObservationFactory.Generate(id, "P1", "E1", effective, seed: 20260825 + i, specimenIds, observationIds);
            var json = JsonSerializer.Serialize(obs, obs.GetType(), serializerOptions);
            entries.Add(Entry("Observation", id, json));
        }

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1",
            entries,
            null,
            QueryPlanDefaults.GetDefaultAsInput(),
            "2023-01-15T00:00:00Z",
            "2023-01-15T23:59:59Z",
            allowEncounterAnchoredDateOverrideForOutOfRange: false);

        var mismatches = new List<string>();
        foreach (var entry in entries.Where(e => e.ResourceType == "Observation"))
        {
            var overlaps = ObservationJsonOverlaps(entry.Resource, mpStart, mpEnd);
            var simHas = acquired.Contains(entry.Key);
            if (overlaps != simHas)
                mismatches.Add($"{entry.Key} overlaps={overlaps} sim={simHas} json={entry.Resource.GetRawText()}");
        }

        Assert.True(mismatches.Count == 0,
            $"Simulator/overlap mismatches ({mismatches.Count}):\n" + string.Join("\n", mismatches.Take(8)));
    }

    private static bool ObservationJsonOverlaps(JsonElement resource, DateTimeOffset mpStart, DateTimeOffset mpEnd)
    {
        DateTimeOffset start = default;
        DateTimeOffset end = default;
        if (resource.TryGetProperty("effectiveDateTime", out var dt)
            && dt.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(dt.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out start))
        {
            end = start;
        }
        else if (resource.TryGetProperty("effectivePeriod", out var period) && period.ValueKind == JsonValueKind.Object)
        {
            var hasStart = period.TryGetProperty("start", out var s)
                           && s.ValueKind == JsonValueKind.String
                           && DateTimeOffset.TryParse(s.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out start);
            var hasEnd = period.TryGetProperty("end", out var e)
                         && e.ValueKind == JsonValueKind.String
                         && DateTimeOffset.TryParse(e.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out end);
            if (!hasStart && !hasEnd)
                return false;
            if (!hasStart) start = DateTimeOffset.MinValue;
            if (!hasEnd) end = DateTimeOffset.MaxValue;
        }
        else
        {
            return false;
        }

        return end >= mpStart && start <= mpEnd;
    }

    [Fact]
    public void Observation_EffectiveInLastSecondOfPeriodEnd_IsAcquired()
    {
        // Daily ACH PeriodEnd is formatted as le2023-01-15T23:59:59Z (second precision).
        // HAPI treats that bound as covering [23:59:59.000, 24:00:00). A generated
        // Observation at 23:59:59.167 is included by DA and must be predicted.
        var encounter = Entry("Encounter", "E1", """
            { "resourceType":"Encounter","id":"E1",
              "period": { "start":"2023-01-14T18:00:00Z", "end":"2023-01-16T05:59:59Z" } }
            """);
        var inLastSecond = Entry("Observation", "O-last-second", """
            { "resourceType":"Observation","id":"O-last-second",
              "category":[{"coding":[{"system":"http://terminology.hl7.org/CodeSystem/observation-category","code":"vital-signs"}]}],
              "encounter":{"reference":"Encounter/E1"},
              "effectiveDateTime":"2023-01-15T23:59:59.167Z" }
            """);
        var atNextMidnight = Entry("Observation", "O-next-midnight", """
            { "resourceType":"Observation","id":"O-next-midnight",
              "category":[{"coding":[{"system":"http://terminology.hl7.org/CodeSystem/observation-category","code":"vital-signs"}]}],
              "encounter":{"reference":"Encounter/E1"},
              "effectiveDateTime":"2023-01-16T00:00:00Z" }
            """);

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            "P1",
            [encounter, inLastSecond, atNextMidnight],
            null,
            QueryPlanDefaults.GetDefaultAsInput(),
            "2023-01-15T00:00:00Z",
            "2023-01-15T23:59:59Z",
            allowEncounterAnchoredDateOverrideForOutOfRange: false);

        Assert.Contains("Observation/O-last-second", acquired);
        Assert.DoesNotContain("Observation/O-next-midnight", acquired);
    }
}
