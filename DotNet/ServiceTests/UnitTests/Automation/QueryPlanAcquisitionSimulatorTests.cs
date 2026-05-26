using LantanaGroup.Automation.Generation;
using LantanaGroup.Automation.Helpers;
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
        // Lab observations in the synthetic generator (and many real EHRs) use
        // effectivePeriod, not effectiveDateTime.
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
}
