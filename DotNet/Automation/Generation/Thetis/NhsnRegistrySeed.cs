using System.Text.Json;
using Thetis.Generation.Abstractions;

namespace LantanaGroup.Automation.Generation.Thetis;

/// <summary>
/// In-memory clinical lookup seed for library-hosted Thetis generation.
/// Ports <see cref="ScenarioResourceMap"/> (universal inpatient set + per-dx
/// palette) onto condition-triggered medication / lab-panel / procedure rows
/// so a pneumonia stay does not resolve insulin.
/// </summary>
public static class NhsnRegistrySeed
{
    private const string Snomed = "http://snomed.info/sct";
    private const string RxNorm = "http://www.nlm.nih.gov/research/umls/rxnorm";
    private const string ConditionTrigger = "condition";

    public static IReadOnlyList<ConditionLookupRecord> Conditions { get; } =
        FhirGenerationCodes.ClinicalScenarios.Select(s => new ConditionLookupRecord
        {
            Id = s.ScenarioId,
            ConditionSystem = Snomed,
            ConditionCode = s.PrimaryDxSnomed,
            ConditionDisplay = s.PrimaryDxDisplay,
            Category = "encounter-diagnosis",
            SelectionWeight = 1
        }).ToList();

    public static IReadOnlyList<MedicationLookupRecord> Medications { get; } = BuildMedications();

    public static IReadOnlyList<LabPanelLookupRecord> LabPanels { get; } = BuildLabPanels();

    public static IReadOnlyList<ProcedureLookupRecord> Procedures { get; } = BuildProcedures();

    private static List<MedicationLookupRecord> BuildMedications()
    {
        var list = new List<MedicationLookupRecord>();
        var pool = FhirGenerationCodes.Medications;
        for (var i = 0; i < FhirGenerationCodes.ClinicalScenarios.Length; i++)
        {
            var scenario = FhirGenerationCodes.ClinicalScenarios[i];
            AddMedicationIndices(
                list, scenario,
                ScenarioResourceMap.UniversalMedicationIndices,
                selectionWeight: 1);
            var scenarioMeds = ScenarioResourceMap.ScenarioMedicationIndices[i];
            AddMedicationIndices(list, scenario, scenarioMeds, selectionWeight: 8);
        }

        return list;

        void AddMedicationIndices(
            List<MedicationLookupRecord> target,
            FhirGenerationCodes.ClinicalScenarioDefinition scenario,
            int[] indices,
            double selectionWeight)
        {
            foreach (var idx in indices)
            {
                if (idx < 0 || idx >= pool.Length)
                    continue;
                var med = pool[idx];
                target.Add(new MedicationLookupRecord
                {
                    Id = Guid.NewGuid(),
                    TriggerType = ConditionTrigger,
                    TriggerSystem = Snomed,
                    TriggerCode = scenario.PrimaryDxSnomed,
                    TriggerDisplay = scenario.PrimaryDxDisplay,
                    MedicationSystem = RxNorm,
                    MedicationCode = med.RxCode,
                    MedicationDisplay = med.Display,
                    SelectionWeight = selectionWeight
                });
            }
        }
    }

    private static List<LabPanelLookupRecord> BuildLabPanels()
    {
        var list = new List<LabPanelLookupRecord>();
        for (var i = 0; i < FhirGenerationCodes.ClinicalScenarios.Length; i++)
        {
            var scenario = FhirGenerationCodes.ClinicalScenarios[i];
            var obsIndices = ScenarioResourceMap.GetMergedIndices(
                ScenarioResourceMap.UniversalObservationIndices,
                ScenarioResourceMap.ScenarioObservationIndices,
                i,
                FhirGenerationCodes.Observations.Length);
            var specIndices = ScenarioResourceMap.GetMergedIndices(
                ScenarioResourceMap.UniversalSpecimenIndices,
                ScenarioResourceMap.ScenarioSpecimenIndices,
                i,
                FhirGenerationCodes.Specimens.Length);
            var specimen = specIndices.Length > 0
                ? FhirGenerationCodes.Specimens[specIndices[0]]
                : FhirGenerationCodes.Specimens[0];

            var srIndices = ScenarioResourceMap.GetMergedIndices(
                ScenarioResourceMap.UniversalServiceRequestIndices,
                ScenarioResourceMap.ScenarioServiceRequestIndices,
                i,
                FhirGenerationCodes.ServiceRequests.Length);
            var labOrder = srIndices
                .Select(idx => FhirGenerationCodes.ServiceRequests[idx])
                .FirstOrDefault(sr => sr.IsLab);
            var panelLoinc = string.IsNullOrWhiteSpace(labOrder.Code) ? "58410-2" : labOrder.Code;
            var panelDisplay = string.IsNullOrWhiteSpace(labOrder.Display)
                ? "CBC panel - Blood by Automated count"
                : labOrder.Display;

            var labObsIndices = obsIndices
                .Where(idx => string.Equals(
                    FhirGenerationCodes.Observations[idx].Category, "laboratory", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            list.Add(new LabPanelLookupRecord
            {
                Id = Guid.NewGuid(),
                TriggerType = ConditionTrigger,
                TriggerSystem = Snomed,
                TriggerCode = scenario.PrimaryDxSnomed,
                TriggerDisplay = scenario.PrimaryDxDisplay,
                PanelCode = $"dx-{scenario.PrimaryDxSnomed}",
                PanelLoincCode = panelLoinc,
                PanelLoincDisplay = panelDisplay,
                Tests = TestsFromObservationIndices(labObsIndices.Length > 0 ? labObsIndices : obsIndices),
                SelectionWeight = 1,
                PanelCategory = "laboratory",
                GenerateDiagnosticReport = true,
                SpecimenTypeCode = specimen.TypeCode,
                SpecimenTypeDisplay = specimen.TypeDisplay
            });
        }

        return list;
    }

    private static List<ProcedureLookupRecord> BuildProcedures()
    {
        var list = new List<ProcedureLookupRecord>();
        var pool = FhirGenerationCodes.Procedures;
        for (var i = 0; i < FhirGenerationCodes.ClinicalScenarios.Length; i++)
        {
            foreach (var idx in ScenarioResourceMap.ScenarioProcedureIndices[i])
            {
                if (idx < 0 || idx >= pool.Length)
                    continue;
                var proc = pool[idx];
                list.Add(new ProcedureLookupRecord
                {
                    Id = Guid.NewGuid(),
                    ProcedureSystem = Snomed,
                    ProcedureCode = proc.Code,
                    ProcedureDisplay = proc.Display,
                    Category = "surgical",
                    SelectionWeight = 1
                });
            }
        }

        return list;
    }

    private static JsonDocument TestsFromObservationIndices(int[] indices)
    {
        var tests = new List<Dictionary<string, object?>>(indices.Length);
        foreach (var idx in indices)
        {
            if (idx < 0 || idx >= FhirGenerationCodes.Observations.Length)
                continue;
            var obs = FhirGenerationCodes.Observations[idx];
            var test = new Dictionary<string, object?>
            {
                ["loincCode"] = obs.Code,
                ["loincDisplay"] = obs.Display
            };
            if (string.IsNullOrWhiteSpace(obs.Unit) || obs.Unit is "SNOMED-CT" or "{score}")
            {
                test["codedResults"] = new object[]
                {
                    new Dictionary<string, string>
                    {
                        ["code"] = "260385009",
                        ["system"] = Snomed,
                        ["display"] = "Negative"
                    }
                };
            }
            else
            {
                test["unit"] = obs.Unit;
                test["unitCode"] = obs.Unit;
                test["minValue"] = obs.NormLow;
                test["maxValue"] = obs.NormHigh;
            }

            tests.Add(test);
        }

        return JsonDocument.Parse(JsonSerializer.Serialize(tests));
    }
}
