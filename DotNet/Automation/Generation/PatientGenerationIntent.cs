using Thetis.Generation.Abstractions;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Clinical shape for generation. Null / empty fields use fixture defaults
/// (inpatient class, Encounter Inpatient type, home discharge). Palettes are
/// the codes that will be generated — empty means no extra story-pack codes.
/// Unset is not the same as an empty palette.
/// </summary>
public sealed class PatientGenerationIntent
{
    public string? Gender { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }

    public string? EncounterClass { get; set; }
    public string? EncounterStatus { get; set; }
    public int? DurationMinutes { get; set; }
    public string? DischargeDisposition { get; set; }
    public bool? IncludeHospitalization { get; set; }

    public string? PrimaryConditionSnomed { get; set; }
    public string? PrimaryConditionDisplay { get; set; }
    public string? ConditionCategory { get; set; }
    public List<CodedPaletteItem>? ConditionPalette { get; set; }
    public PaletteMode ConditionPaletteMode { get; set; } = PaletteMode.Inherit;
    public int? AdditionalConditionCount { get; set; }
    public bool? GenerateLabWork { get; set; }

    public List<ObservationPaletteItem>? ObservationPalette { get; set; }
    public PaletteMode ObservationPaletteMode { get; set; } = PaletteMode.Inherit;
    public bool? SpreadObservationsAcrossEncounter { get; set; }

    public bool? IncludeAllergy { get; set; }
    public bool? IncludeConditionDrivenMedications { get; set; }
    public bool? IncludeHypoglycemicInsulin { get; set; }

    public Dictionary<string, int>? ResourceTypeCounts { get; set; }

    public List<CodedPaletteItem>? ProcedurePalette { get; set; }
    public PaletteMode ProcedurePaletteMode { get; set; } = PaletteMode.Inherit;

    public string? MedicationAdministrationRxNorm { get; set; }
    public string? MedicationAdministrationDisplay { get; set; }

    public string? ServiceRequestLoinc { get; set; }
    public string? ServiceRequestDisplay { get; set; }
    public string? SpecimenTypeCode { get; set; }
    public string? SpecimenTypeDisplay { get; set; }
    public string? DiagnosticReportLoinc { get; set; }
    public string? DiagnosticReportDisplay { get; set; }

    /// <summary>
    /// Overlay <paramref name="over"/> onto <paramref name="under"/>. Non-null
    /// fields on <paramref name="over"/> win. Palettes with mode Inherit leave
    /// the under palette unless <paramref name="over"/> also supplies a list.
    /// </summary>
    public static PatientGenerationIntent? Merge(PatientGenerationIntent? under, PatientGenerationIntent? over)
    {
        if (under == null)
            return Clone(over);
        if (over == null)
            return Clone(under);

        return new PatientGenerationIntent
        {
            Gender = over.Gender ?? under.Gender,
            MinAge = over.MinAge ?? under.MinAge,
            MaxAge = over.MaxAge ?? under.MaxAge,
            EncounterClass = over.EncounterClass ?? under.EncounterClass,
            EncounterStatus = over.EncounterStatus ?? under.EncounterStatus,
            DurationMinutes = over.DurationMinutes ?? under.DurationMinutes,
            DischargeDisposition = over.DischargeDisposition ?? under.DischargeDisposition,
            IncludeHospitalization = over.IncludeHospitalization ?? under.IncludeHospitalization,
            PrimaryConditionSnomed = over.PrimaryConditionSnomed ?? under.PrimaryConditionSnomed,
            PrimaryConditionDisplay = over.PrimaryConditionDisplay ?? under.PrimaryConditionDisplay,
            ConditionCategory = over.ConditionCategory ?? under.ConditionCategory,
            ConditionPalette = MergeCoded(under.ConditionPalette, over.ConditionPalette, over.ConditionPaletteMode),
            ConditionPaletteMode = over.ConditionPalette is { Count: > 0 } || over.ConditionPaletteMode != PaletteMode.Inherit
                ? over.ConditionPaletteMode
                : under.ConditionPaletteMode,
            AdditionalConditionCount = over.AdditionalConditionCount ?? under.AdditionalConditionCount,
            GenerateLabWork = over.GenerateLabWork ?? under.GenerateLabWork,
            ObservationPalette = MergeObservations(under.ObservationPalette, over.ObservationPalette, over.ObservationPaletteMode),
            ObservationPaletteMode = over.ObservationPalette is { Count: > 0 } || over.ObservationPaletteMode != PaletteMode.Inherit
                ? over.ObservationPaletteMode
                : under.ObservationPaletteMode,
            SpreadObservationsAcrossEncounter = over.SpreadObservationsAcrossEncounter ?? under.SpreadObservationsAcrossEncounter,
            IncludeAllergy = over.IncludeAllergy ?? under.IncludeAllergy,
            IncludeConditionDrivenMedications = over.IncludeConditionDrivenMedications ?? under.IncludeConditionDrivenMedications,
            IncludeHypoglycemicInsulin = over.IncludeHypoglycemicInsulin ?? under.IncludeHypoglycemicInsulin,
            ResourceTypeCounts = over.ResourceTypeCounts ?? under.ResourceTypeCounts,
            ProcedurePalette = MergeCoded(under.ProcedurePalette, over.ProcedurePalette, over.ProcedurePaletteMode),
            ProcedurePaletteMode = over.ProcedurePalette is { Count: > 0 } || over.ProcedurePaletteMode != PaletteMode.Inherit
                ? over.ProcedurePaletteMode
                : under.ProcedurePaletteMode,
            MedicationAdministrationRxNorm = over.MedicationAdministrationRxNorm ?? under.MedicationAdministrationRxNorm,
            MedicationAdministrationDisplay = over.MedicationAdministrationDisplay ?? under.MedicationAdministrationDisplay,
            ServiceRequestLoinc = over.ServiceRequestLoinc ?? under.ServiceRequestLoinc,
            ServiceRequestDisplay = over.ServiceRequestDisplay ?? under.ServiceRequestDisplay,
            SpecimenTypeCode = over.SpecimenTypeCode ?? under.SpecimenTypeCode,
            SpecimenTypeDisplay = over.SpecimenTypeDisplay ?? under.SpecimenTypeDisplay,
            DiagnosticReportLoinc = over.DiagnosticReportLoinc ?? under.DiagnosticReportLoinc,
            DiagnosticReportDisplay = over.DiagnosticReportDisplay ?? under.DiagnosticReportDisplay
        };
    }

    public static PatientGenerationIntent? Clone(PatientGenerationIntent? source)
    {
        if (source == null)
            return null;

        return new PatientGenerationIntent
        {
            Gender = source.Gender,
            MinAge = source.MinAge,
            MaxAge = source.MaxAge,
            EncounterClass = source.EncounterClass,
            EncounterStatus = source.EncounterStatus,
            DurationMinutes = source.DurationMinutes,
            DischargeDisposition = source.DischargeDisposition,
            IncludeHospitalization = source.IncludeHospitalization,
            PrimaryConditionSnomed = source.PrimaryConditionSnomed,
            PrimaryConditionDisplay = source.PrimaryConditionDisplay,
            ConditionCategory = source.ConditionCategory,
            ConditionPalette = source.ConditionPalette?.ToList(),
            ConditionPaletteMode = source.ConditionPaletteMode,
            AdditionalConditionCount = source.AdditionalConditionCount,
            GenerateLabWork = source.GenerateLabWork,
            ObservationPalette = source.ObservationPalette?.ToList(),
            ObservationPaletteMode = source.ObservationPaletteMode,
            SpreadObservationsAcrossEncounter = source.SpreadObservationsAcrossEncounter,
            IncludeAllergy = source.IncludeAllergy,
            IncludeConditionDrivenMedications = source.IncludeConditionDrivenMedications,
            IncludeHypoglycemicInsulin = source.IncludeHypoglycemicInsulin,
            ResourceTypeCounts = source.ResourceTypeCounts is null
                ? null
                : new Dictionary<string, int>(source.ResourceTypeCounts, StringComparer.OrdinalIgnoreCase),
            ProcedurePalette = source.ProcedurePalette?.ToList(),
            ProcedurePaletteMode = source.ProcedurePaletteMode,
            MedicationAdministrationRxNorm = source.MedicationAdministrationRxNorm,
            MedicationAdministrationDisplay = source.MedicationAdministrationDisplay,
            ServiceRequestLoinc = source.ServiceRequestLoinc,
            ServiceRequestDisplay = source.ServiceRequestDisplay,
            SpecimenTypeCode = source.SpecimenTypeCode,
            SpecimenTypeDisplay = source.SpecimenTypeDisplay,
            DiagnosticReportLoinc = source.DiagnosticReportLoinc,
            DiagnosticReportDisplay = source.DiagnosticReportDisplay
        };
    }

    private static List<ObservationPaletteItem>? MergeObservations(
        List<ObservationPaletteItem>? under,
        List<ObservationPaletteItem>? over,
        PaletteMode overMode)
        => MergePalette(under, over, overMode, i => i.LoincCode);

    private static List<CodedPaletteItem>? MergeCoded(
        List<CodedPaletteItem>? under,
        List<CodedPaletteItem>? over,
        PaletteMode overMode)
        => MergePalette(under, over, overMode, i => i.Code);

    private static List<T>? MergePalette<T>(
        List<T>? under,
        List<T>? over,
        PaletteMode overMode,
        Func<T, string> key)
    {
        if (overMode is PaletteMode.Replace)
            return over?.ToList();
        if (over is null)
            return under?.ToList();
        if (overMode is PaletteMode.Inherit)
            return over.Count > 0 ? over.ToList() : under?.ToList();

        if (under is null || under.Count == 0)
            return over.ToList();

        var merged = under.ToList();
        var seen = new HashSet<string>(merged.Select(key), StringComparer.OrdinalIgnoreCase);
        foreach (var item in over)
        {
            if (seen.Add(key(item)))
                merged.Add(item);
        }

        return merged;
    }

    /// <summary>
    /// True when any field is set. Empty intent means fixture defaults, not a story pack.
    /// </summary>
    public bool HasOverlays()
    {
        return Gender != null
            || MinAge != null
            || MaxAge != null
            || EncounterClass != null
            || EncounterStatus != null
            || DurationMinutes != null
            || DischargeDisposition != null
            || IncludeHospitalization != null
            || PrimaryConditionSnomed != null
            || PrimaryConditionDisplay != null
            || ConditionCategory != null
            || ConditionPalette is { Count: > 0 }
            || ConditionPaletteMode != PaletteMode.Inherit
            || AdditionalConditionCount != null
            || GenerateLabWork != null
            || ObservationPalette is { Count: > 0 }
            || ObservationPaletteMode != PaletteMode.Inherit
            || SpreadObservationsAcrossEncounter != null
            || IncludeAllergy != null
            || IncludeConditionDrivenMedications != null
            || IncludeHypoglycemicInsulin != null
            || ResourceTypeCounts is { Count: > 0 }
            || ProcedurePalette is { Count: > 0 }
            || ProcedurePaletteMode != PaletteMode.Inherit
            || MedicationAdministrationRxNorm != null
            || MedicationAdministrationDisplay != null
            || ServiceRequestLoinc != null
            || ServiceRequestDisplay != null
            || SpecimenTypeCode != null
            || SpecimenTypeDisplay != null
            || DiagnosticReportLoinc != null
            || DiagnosticReportDisplay != null;
    }
}

public enum PaletteMode
{
    Inherit = 0,
    Replace = 1,
    Append = 2
}
