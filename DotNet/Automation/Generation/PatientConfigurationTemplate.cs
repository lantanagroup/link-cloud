using LantanaGroup.Automation.Generation.Thetis;
using Thetis.Generation.Abstractions;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Turns a Clinical Profile (story pack) into a fully populated
/// <see cref="PatientGenerationIntent"/> so the Patient Configuration editor
/// can show the codes, encounter, and mix the generator will use.
/// </summary>
public static class PatientConfigurationTemplate
{
    public static PatientGenerationIntent FromClinicalProfile(
        FhirGenerationCodes.ClinicalScenarioDefinition scenario,
        int totalResourcesPerPatient = 50,
        bool inpatient = true,
        bool hypo = false)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var spec = PatientSpecFactory.FromScenario(
            scenario,
            inpatient,
            hypo,
            Math.Max(1, totalResourcesPerPatient));
        return FromSpec(spec);
    }

    public static PatientGenerationIntent FromSpec(PatientGenerationSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        return new PatientGenerationIntent
        {
            Gender = string.IsNullOrWhiteSpace(spec.PatientGender) ? "random" : spec.PatientGender,
            MinAge = spec.PatientMinAge,
            MaxAge = spec.PatientMaxAge,
            EncounterClass = spec.EncounterClass,
            EncounterStatus = spec.EncounterStatus,
            DurationMinutes = spec.DurationMinutes,
            DischargeDisposition = spec.DischargeDisposition,
            IncludeHospitalization = spec.IncludeHospitalization,
            PrimaryConditionSnomed = spec.PrimaryConditionSnomed,
            PrimaryConditionDisplay = spec.PrimaryConditionDisplay,
            ConditionCategory = spec.ConditionCategory,
            ConditionPalette = spec.ConditionPalette.ToList(),
            ConditionPaletteMode = PaletteMode.Replace,
            AdditionalConditionCount = spec.AdditionalConditionCount,
            GenerateLabWork = spec.GenerateLabWork,
            ObservationPalette = spec.ObservationPalette.ToList(),
            ObservationPaletteMode = PaletteMode.Replace,
            SpreadObservationsAcrossEncounter = spec.SpreadObservationsAcrossEncounter,
            IncludeAllergy = spec.IncludeAllergy,
            IncludeConditionDrivenMedications = spec.IncludeConditionDrivenMedications,
            IncludeHypoglycemicInsulin = spec.IncludeMedicationRequest,
            ProcedurePalette = spec.ProcedurePalette.ToList(),
            ProcedurePaletteMode = PaletteMode.Replace,
            MedicationAdministrationRxNorm = spec.MedicationAdministrationRxNorm,
            MedicationAdministrationDisplay = spec.MedicationAdministrationDisplay,
            ServiceRequestLoinc = spec.ServiceRequestLoinc,
            ServiceRequestDisplay = spec.ServiceRequestDisplay,
            SpecimenTypeCode = spec.SpecimenTypeCode,
            SpecimenTypeDisplay = spec.SpecimenTypeDisplay,
            DiagnosticReportLoinc = spec.DiagnosticReportLoinc,
            DiagnosticReportDisplay = spec.DiagnosticReportDisplay
        };
    }

    public static Dictionary<string, int> ExampleResourceCounts(PatientGenerationSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Observation"] = spec.ObservationCount,
            ["Condition"] = spec.AdditionalConditionCount + 1,
            ["Procedure"] = spec.ProcedureCount,
            ["MedicationRequest"] = spec.MedicationRequestCount,
            ["MedicationAdministration"] = spec.MedicationAdministrationCount,
            ["Coverage"] = spec.CoverageCount,
            ["ServiceRequest"] = spec.ServiceRequestCount,
            ["Specimen"] = spec.SpecimenCount,
            ["DiagnosticReport"] = spec.DiagnosticReportCount
        };
    }
}
