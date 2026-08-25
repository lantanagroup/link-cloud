using Thetis.Generation.Abstractions;

namespace LantanaGroup.Automation.Generation.Thetis;

/// <summary>
/// Maps Automation scenario/profile knobs onto a Thetis <see cref="PatientGenerationSpec"/>.
/// The Engine compiler turns that spec into a DAG; this factory contains no node JSON.
/// </summary>
public static class PatientSpecFactory
{
    public const string HypoInsulinMedicationIdVar = "hypoInsulinMedicationId";
    public const string LocationIdVar = "icuLocationId";

    private static readonly Dictionary<string, string> VitalLoincToType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["8867-4"] = "heart-rate",
        ["8310-5"] = "body-temperature",
        ["59408-5"] = "oxygen-saturation",
        ["8302-2"] = "body-height",
        ["29463-7"] = "body-weight",
        ["55284-4"] = "blood-pressure",
        ["9279-1"] = "respiratory-rate"
    };

    public static PatientGenerationSpec From(
        PatientProfile profile,
        FhirGenerationCodes.ClinicalScenarioDefinition scenario,
        int totalResourcesPerPatient,
        FhirGenerationConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(scenario);

        var inpatient = profile.RequiresInpatientEncounter();
        var hypo = profile.RequiresHypoglycemicMedication();
        var spec = FromScenario(scenario, inpatient, hypo, totalResourcesPerPatient, config);
        return ApplyIntent(spec, profile.Intent, hypo);
    }

    public static PatientGenerationSpec FromScenario(
        FhirGenerationCodes.ClinicalScenarioDefinition scenario,
        bool inpatient = true,
        bool hypo = false,
        int totalResourcesPerPatient = 10,
        FhirGenerationConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var n = Math.Max(1, totalResourcesPerPatient);
        var dist = (config ?? new FhirGenerationConfig()).ResourceDistribution;
        int Count(string type, int fallback = 0)
        {
            if (dist != null && dist.TryGetValue(type, out var fraction) && fraction > 0)
                return Math.Max(1, (int)(n * fraction));
            return fallback;
        }

        var scenarioIdx = FhirGenerationCodes.GetScenarioArrayPosition(scenario);
        var observationPalette = BuildObservationPalette(scenarioIdx);
        var conditionPalette = BuildConditionPalette(scenarioIdx);
        var procedurePalette = BuildProcedurePalette(scenarioIdx);
        var (srLoinc, srDisplay) = FirstLabServiceRequest(scenarioIdx);
        var (specimenCode, specimenDisplay) = FirstSpecimen(scenarioIdx);
        var (adminRx, adminDisplay) = FirstMedication(scenarioIdx);

        var observationCount = Count("Observation", 10);
        var conditionCount = Count("Condition");
        var procedureCount = procedurePalette.Count > 0 ? Count("Procedure") : 0;
        var medReqCount = Count("MedicationRequest", 5);
        var medAdminCount = Count("MedicationAdministration");
        var coverageCount = Count("Coverage");
        var srCount = Count("ServiceRequest");
        var specimenCount = Count("Specimen");
        var dxCount = Count("DiagnosticReport");

        // Anchors are always emitted (Patient, Encounter, primary Condition, Device, CareTeam, CarePlan, List).
        const int anchors = 7;
        var allocated = observationCount
            + Math.Max(0, conditionCount - 1)
            + procedureCount
            + medReqCount
            + medAdminCount
            + coverageCount
            + srCount
            + specimenCount
            + dxCount
            + anchors
            + (hypo ? 1 : 0);
        if (allocated < n)
            observationCount += n - allocated;

        return new PatientGenerationSpec
        {
            EncounterClass = inpatient ? "IMP" : "AMB",
            EncounterStatus = "finished",
            LocationIdVar = LocationIdVar,
            DischargeDisposition = string.IsNullOrWhiteSpace(scenario.DischargeDispositionCode)
                ? "home"
                : scenario.DischargeDispositionCode,
            IncludeHospitalization = true,
            PrimaryConditionSnomed = scenario.PrimaryDxSnomed,
            PrimaryConditionDisplay = scenario.PrimaryDxDisplay,
            ConditionCategory = "encounter-diagnosis",
            GenerateLabWork = true,
            ConditionPalette = conditionPalette,
            AdditionalConditionCount = Math.Max(0, conditionCount - 1),
            ObservationType = "vital-signs",
            ObservationCount = Math.Max(1, observationCount),
            ObservationPalette = observationPalette,
            SpreadObservationsAcrossEncounter = inpatient,
            IncludeConditionDrivenMedications = true,
            MedicationRequestCount = Math.Max(1, medReqCount),
            MedicationAdministrationCount = medAdminCount,
            MedicationAdministrationRxNorm = adminRx,
            MedicationAdministrationDisplay = adminDisplay,
            IncludeMedicationRequest = hypo,
            MedicationIdVar = hypo ? HypoInsulinMedicationIdVar : null,
            IncludeProcedure = procedurePalette.Count > 0,
            ProcedureCount = procedureCount,
            ProcedurePalette = procedurePalette,
            ProcedureSnomed = procedurePalette.Count > 0 ? procedurePalette[0].Code : null,
            ProcedureDisplay = procedurePalette.Count > 0 ? procedurePalette[0].Display : null,
            CoverageCount = coverageCount,
            ServiceRequestCount = srCount,
            ServiceRequestLoinc = srLoinc,
            ServiceRequestDisplay = srDisplay,
            SpecimenCount = specimenCount,
            SpecimenTypeCode = specimenCode,
            SpecimenTypeDisplay = specimenDisplay,
            DiagnosticReportCount = dxCount,
            DiagnosticReportLoinc = srLoinc,
            DiagnosticReportDisplay = srDisplay
        };
    }

    internal static PatientGenerationSpec ApplyIntent(
        PatientGenerationSpec spec,
        PatientGenerationIntent? intent,
        bool hypoFromEligibility)
    {
        if (intent == null)
            return spec;

        var observationPalette = CombinePalette(
            spec.ObservationPalette,
            intent.ObservationPalette,
            intent.ObservationPaletteMode);
        var conditionPalette = CombineCoded(
            spec.ConditionPalette,
            intent.ConditionPalette,
            intent.ConditionPaletteMode);
        var procedurePalette = CombineCoded(
            spec.ProcedurePalette,
            intent.ProcedurePalette,
            intent.ProcedurePaletteMode);

        int Count(string type, int current)
        {
            if (intent.ResourceTypeCounts != null
                && intent.ResourceTypeCounts.TryGetValue(type, out var n)
                && n >= 0)
            {
                return n;
            }

            return current;
        }

        var observationCount = Count("Observation", spec.ObservationCount);
        var additionalConditions = intent.AdditionalConditionCount
            ?? spec.AdditionalConditionCount;
        if (intent.ResourceTypeCounts != null
            && intent.ResourceTypeCounts.TryGetValue("Condition", out var conditionTotal)
            && conditionTotal >= 0)
        {
            additionalConditions = Math.Max(0, conditionTotal - 1);
        }

        var includeHypo = intent.IncludeHypoglycemicInsulin ?? (hypoFromEligibility && spec.IncludeMedicationRequest);
        var encounterClass = string.IsNullOrWhiteSpace(intent.EncounterClass)
            ? spec.EncounterClass
            : intent.EncounterClass!;
        var encounterStatus = string.IsNullOrWhiteSpace(intent.EncounterStatus)
            ? spec.EncounterStatus
            : intent.EncounterStatus!;
        var includeHospitalization = intent.IncludeHospitalization
            ?? spec.IncludeHospitalization;
        if (string.Equals(encounterStatus, "in-progress", StringComparison.OrdinalIgnoreCase))
            includeHospitalization = false;

        return new PatientGenerationSpec
        {
            EncounterClass = encounterClass,
            EncounterStatus = encounterStatus,
            DurationMinutes = intent.DurationMinutes ?? spec.DurationMinutes,
            LocationIdVar = spec.LocationIdVar,
            DischargeDisposition = string.IsNullOrWhiteSpace(intent.DischargeDisposition)
                ? spec.DischargeDisposition
                : intent.DischargeDisposition,
            IncludeHospitalization = includeHospitalization,
            PatientGender = string.IsNullOrWhiteSpace(intent.Gender) || string.Equals(intent.Gender, "random", StringComparison.OrdinalIgnoreCase)
                ? spec.PatientGender
                : intent.Gender,
            PatientMinAge = intent.MinAge ?? spec.PatientMinAge,
            PatientMaxAge = intent.MaxAge ?? spec.PatientMaxAge,
            PrimaryConditionSnomed = string.IsNullOrWhiteSpace(intent.PrimaryConditionSnomed)
                ? spec.PrimaryConditionSnomed
                : intent.PrimaryConditionSnomed,
            PrimaryConditionDisplay = string.IsNullOrWhiteSpace(intent.PrimaryConditionDisplay)
                ? spec.PrimaryConditionDisplay
                : intent.PrimaryConditionDisplay,
            ConditionCategory = string.IsNullOrWhiteSpace(intent.ConditionCategory)
                ? spec.ConditionCategory
                : intent.ConditionCategory!,
            GenerateLabWork = intent.GenerateLabWork ?? spec.GenerateLabWork,
            ConditionPalette = conditionPalette,
            AdditionalConditionCount = additionalConditions,
            ObservationType = spec.ObservationType,
            ObservationCount = Math.Max(0, observationCount),
            ObservationPalette = observationPalette,
            SpreadObservationsAcrossEncounter = intent.SpreadObservationsAcrossEncounter ?? spec.SpreadObservationsAcrossEncounter,
            IncludeConditionDrivenMedications = intent.IncludeConditionDrivenMedications ?? spec.IncludeConditionDrivenMedications,
            MedicationRequestCount = Count("MedicationRequest", spec.MedicationRequestCount),
            MedicationAdministrationCount = Count("MedicationAdministration", spec.MedicationAdministrationCount),
            MedicationAdministrationRxNorm = string.IsNullOrWhiteSpace(intent.MedicationAdministrationRxNorm)
                ? spec.MedicationAdministrationRxNorm
                : intent.MedicationAdministrationRxNorm,
            MedicationAdministrationDisplay = string.IsNullOrWhiteSpace(intent.MedicationAdministrationDisplay)
                ? spec.MedicationAdministrationDisplay
                : intent.MedicationAdministrationDisplay,
            IncludeMedicationRequest = includeHypo,
            MedicationIdVar = includeHypo ? HypoInsulinMedicationIdVar : null,
            IncludeProcedure = procedurePalette.Count > 0 || spec.IncludeProcedure,
            ProcedureCount = Count("Procedure", spec.ProcedureCount),
            ProcedurePalette = procedurePalette,
            ProcedureSnomed = procedurePalette.Count > 0 ? procedurePalette[0].Code : spec.ProcedureSnomed,
            ProcedureDisplay = procedurePalette.Count > 0 ? procedurePalette[0].Display : spec.ProcedureDisplay,
            CoverageCount = Count("Coverage", spec.CoverageCount),
            ServiceRequestCount = Count("ServiceRequest", spec.ServiceRequestCount),
            ServiceRequestLoinc = string.IsNullOrWhiteSpace(intent.ServiceRequestLoinc)
                ? spec.ServiceRequestLoinc
                : intent.ServiceRequestLoinc,
            ServiceRequestDisplay = string.IsNullOrWhiteSpace(intent.ServiceRequestDisplay)
                ? spec.ServiceRequestDisplay
                : intent.ServiceRequestDisplay,
            SpecimenCount = Count("Specimen", spec.SpecimenCount),
            SpecimenTypeCode = string.IsNullOrWhiteSpace(intent.SpecimenTypeCode)
                ? spec.SpecimenTypeCode
                : intent.SpecimenTypeCode,
            SpecimenTypeDisplay = string.IsNullOrWhiteSpace(intent.SpecimenTypeDisplay)
                ? spec.SpecimenTypeDisplay
                : intent.SpecimenTypeDisplay,
            DiagnosticReportCount = Count("DiagnosticReport", spec.DiagnosticReportCount),
            DiagnosticReportLoinc = string.IsNullOrWhiteSpace(intent.DiagnosticReportLoinc)
                ? spec.DiagnosticReportLoinc
                : intent.DiagnosticReportLoinc,
            DiagnosticReportDisplay = string.IsNullOrWhiteSpace(intent.DiagnosticReportDisplay)
                ? spec.DiagnosticReportDisplay
                : intent.DiagnosticReportDisplay,
            IncludeAllergy = intent.IncludeAllergy ?? spec.IncludeAllergy
        };
    }

    private static IReadOnlyList<ObservationPaletteItem> CombinePalette(
        IReadOnlyList<ObservationPaletteItem> baseline,
        IReadOnlyList<ObservationPaletteItem>? overlay,
        PaletteMode mode)
    {
        if (overlay is not { Count: > 0 })
            return baseline;
        // Inherit + empty list = story default. Inherit + codes is treated as Replace,
        // matching the configurator UI which auto-promotes Inherit → Replace on pick.
        if (mode is PaletteMode.Replace or PaletteMode.Inherit)
            return overlay.ToList();

        var merged = baseline.ToList();
        var seen = new HashSet<string>(merged.Select(i => i.LoincCode), StringComparer.OrdinalIgnoreCase);
        foreach (var item in overlay)
        {
            if (seen.Add(item.LoincCode))
                merged.Add(item);
        }

        return merged;
    }

    private static IReadOnlyList<CodedPaletteItem> CombineCoded(
        IReadOnlyList<CodedPaletteItem> baseline,
        IReadOnlyList<CodedPaletteItem>? overlay,
        PaletteMode mode)
    {
        if (overlay is not { Count: > 0 })
            return baseline;
        if (mode is PaletteMode.Replace or PaletteMode.Inherit)
            return overlay.ToList();

        var merged = baseline.ToList();
        var seen = new HashSet<string>(merged.Select(i => i.Code), StringComparer.OrdinalIgnoreCase);
        foreach (var item in overlay)
        {
            if (seen.Add(item.Code))
                merged.Add(item);
        }

        return merged;
    }

    private static List<ObservationPaletteItem> BuildObservationPalette(int scenarioIdx)
    {
        var list = new List<ObservationPaletteItem>();
        if (scenarioIdx < 0)
            return list;

        var indices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalObservationIndices,
            ScenarioResourceMap.ScenarioObservationIndices,
            scenarioIdx,
            FhirGenerationCodes.Observations.Length);

        foreach (var idx in indices)
        {
            var obs = FhirGenerationCodes.Observations[idx];
            if (VitalLoincToType.TryGetValue(obs.Code, out var vitalType))
            {
                list.Add(new ObservationPaletteItem
                {
                    LoincCode = obs.Code,
                    LoincDisplay = obs.Display,
                    Type = vitalType
                });
                continue;
            }

            var item = new ObservationPaletteItem
            {
                LoincCode = obs.Code,
                LoincDisplay = obs.Display,
                Type = string.IsNullOrWhiteSpace(obs.Category) ? "laboratory" : obs.Category
            };

            if (string.Equals(obs.Unit, "SNOMED-CT", StringComparison.OrdinalIgnoreCase))
            {
                item = new ObservationPaletteItem
                {
                    LoincCode = obs.Code,
                    LoincDisplay = obs.Display,
                    Type = item.Type,
                    ValueCode = "266919005",
                    ValueSystem = "http://snomed.info/sct",
                    ValueDisplay = "Never smoker"
                };
            }
            else if (!string.IsNullOrWhiteSpace(obs.Unit))
            {
                item = new ObservationPaletteItem
                {
                    LoincCode = obs.Code,
                    LoincDisplay = obs.Display,
                    Type = item.Type,
                    Unit = obs.Unit,
                    UnitCode = obs.Unit,
                    MinValue = (decimal)obs.NormLow,
                    MaxValue = (decimal)obs.NormHigh
                };
            }

            list.Add(item);
        }

        return list;
    }

    private static List<CodedPaletteItem> BuildConditionPalette(int scenarioIdx)
    {
        var list = new List<CodedPaletteItem>();
        if (scenarioIdx < 0)
            return list;

        var indices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalConditionIndices,
            ScenarioResourceMap.ScenarioConditionIndices,
            scenarioIdx,
            FhirGenerationCodes.Conditions.Length);

        foreach (var idx in indices)
        {
            var c = FhirGenerationCodes.Conditions[idx];
            list.Add(new CodedPaletteItem
            {
                Code = c.Code,
                Display = c.Display,
                Category = c.Category
            });
        }

        return list;
    }

    private static List<CodedPaletteItem> BuildProcedurePalette(int scenarioIdx)
    {
        var list = new List<CodedPaletteItem>();
        if (scenarioIdx < 0)
            return list;

        var procIndices = ScenarioResourceMap.ScenarioProcedureIndices[
            scenarioIdx % ScenarioResourceMap.ScenarioProcedureIndices.Length];
        foreach (var idx in procIndices)
        {
            if (idx < 0 || idx >= FhirGenerationCodes.Procedures.Length)
                continue;
            var p = FhirGenerationCodes.Procedures[idx];
            list.Add(new CodedPaletteItem { Code = p.Code, Display = p.Display });
        }

        return list;
    }

    private static (string? Loinc, string? Display) FirstLabServiceRequest(int scenarioIdx)
    {
        if (scenarioIdx < 0)
            return ("58410-2", "CBC panel - Blood by Automated count");

        var indices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalServiceRequestIndices,
            ScenarioResourceMap.ScenarioServiceRequestIndices,
            scenarioIdx,
            FhirGenerationCodes.ServiceRequests.Length);

        foreach (var idx in indices)
        {
            var sr = FhirGenerationCodes.ServiceRequests[idx];
            if (sr.IsLab)
                return (sr.Code, sr.Display);
        }

        return ("58410-2", "CBC panel - Blood by Automated count");
    }

    private static (string? RxNorm, string? Display) FirstMedication(int scenarioIdx)
    {
        if (scenarioIdx < 0)
            return (FhirGenerationCodes.Medications[0].RxCode, FhirGenerationCodes.Medications[0].Display);

        var indices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalMedicationIndices,
            ScenarioResourceMap.ScenarioMedicationIndices,
            scenarioIdx,
            FhirGenerationCodes.Medications.Length);
        if (indices.Length == 0)
            return (FhirGenerationCodes.Medications[0].RxCode, FhirGenerationCodes.Medications[0].Display);

        var med = FhirGenerationCodes.Medications[indices[0]];
        return (med.RxCode, med.Display);
    }

    private static (string? Code, string? Display) FirstSpecimen(int scenarioIdx)
    {
        if (scenarioIdx < 0)
            return (FhirGenerationCodes.Specimens[0].TypeCode, FhirGenerationCodes.Specimens[0].TypeDisplay);

        var indices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalSpecimenIndices,
            ScenarioResourceMap.ScenarioSpecimenIndices,
            scenarioIdx,
            FhirGenerationCodes.Specimens.Length);
        if (indices.Length == 0)
            return (FhirGenerationCodes.Specimens[0].TypeCode, FhirGenerationCodes.Specimens[0].TypeDisplay);

        var s = FhirGenerationCodes.Specimens[indices[0]];
        return (s.TypeCode, s.TypeDisplay);
    }
}
