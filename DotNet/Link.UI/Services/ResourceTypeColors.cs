namespace Link.UI.Services;

/// <summary>
/// One colour per FHIR resource type, the same in every chart (donut, bars, patient modal, interactive graph).
/// The first 11 are Automation.UI's chart colours; the rest were picked to stay bright and at least
/// Delta-E 15 apart from every other entry. Grey (<see cref="Other"/>) is reserved for the "Other" remainder.
/// </summary>
public static class ResourceTypeColors
{
    public const string Other = "#adb5bd";

    public static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Observation"] = "#4da3ff",
        ["Location"] = "#28a745",
        ["ServiceRequest"] = "#ffc107",
        ["DiagnosticReport"] = "#dc3545",
        ["Condition"] = "#6f42c1",
        ["Coverage"] = "#20c997",
        ["Encounter"] = "#fd7e14",
        ["MeasureReport"] = "#17a2b8",
        ["MedicationRequest"] = "#e83e8c",
        ["Patient"] = "#6610f2",
        ["Procedure"] = "#007bff",
        ["Specimen"] = "#c29ffa",
        ["MedicationAdministration"] = "#e685b5",
        ["Medication"] = "#feb272",
        ["Practitioner"] = "#997404",
        ["Organization"] = "#198754",
        ["Device"] = "#a3e635",
        ["CareTeam"] = "#fb7185",
        ["CarePlan"] = "#d946ef",
        ["List"] = "#65a30d",
        ["AllergyIntolerance"] = "#b45309",
        ["Immunization"] = "#55e830",
        ["DocumentReference"] = "#30e87a",
        ["MedicationDispense"] = "#30e8e8",
        ["MedicationStatement"] = "#c3b322",
        ["PractitionerRole"] = "#c322a3",
        ["RelatedPerson"] = "#8c68cd",
        ["DeviceRequest"] = "#9333ea",
        ["DeviceUseStatement"] = "#e85530",
        ["Goal"] = "#e89f30",
        ["NutritionOrder"] = "#30b1e8",
        ["QuestionnaireResponse"] = "#2263c3",
        ["Questionnaire"] = "#f38468",
        ["ImagingStudy"] = "#f3c968",
        ["Media"] = "#bb68f3",
        ["Composition"] = "#f368e5",
        ["Flag"] = "#3dd5f3",
        ["FamilyMemberHistory"] = "#14b8a6",
        ["RiskAssessment"] = "#3055e8",
        ["Communication"] = "#e83068",
        ["Task"] = "#a3c322",
        ["Provenance"] = "#53c322",
        ["EpisodeOfCare"] = "#2283c3",
        ["Appointment"] = "#92f368",
        ["ClinicalImpression"] = "#68f3a0",
        ["BodyStructure"] = "#f368bb",
        ["Claim"] = "#ea868f",
        ["ExplanationOfBenefit"] = "#8b5cf6",
        ["AdverseEvent"] = "#e8d630",
        ["Group"] = "#22c373",
        ["Bundle"] = "#a322c3",
        ["Substance"] = "#68f3c9",
    };

    /// <summary>Colour for a type: its fixed entry, "Other" grey for the remainder, else a stable hashed hue.</summary>
    public static string For(string? type)
    {
        var name = type?.Trim() ?? "";
        if (string.Equals(name, "Other", StringComparison.OrdinalIgnoreCase))
            return Other;
        return Map.TryGetValue(name, out var color) ? color : Fallback(name);
    }

    /// <summary>Unlisted types: FNV-1a over the lower-cased name to a bright hue (never grey). chart-palette.js mirrors this.</summary>
    public static string Fallback(string name)
    {
        uint hash = 2166136261;
        foreach (var ch in name.ToLowerInvariant())
        {
            hash ^= ch;
            hash *= 16777619;
        }
        return Hsl((int)(hash % 360), 72, 52);
    }

    private static string Hsl(int h, int s, int l)
    {
        double sat = s / 100.0, light = l / 100.0;
        double a = sat * Math.Min(light, 1 - light);
        double F(int n)
        {
            var k = (n + h / 30.0) % 12;
            return light - a * Math.Max(-1, Math.Min(k - 3, Math.Min(9 - k, 1)));
        }
        return "#" + string.Concat(new[] { F(0), F(8), F(4) }.Select(v => ((int)Math.Round(v * 255)).ToString("x2")));
    }
}