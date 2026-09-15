namespace LantanaGroup.Link.Nhsn.App.Bff.Settings;

// Canonical ValueSet urls the Encounter Mapping step's CPT/SNOMED reference list is expanded
// from, keyed by the label shown in the UI (e.g. "CPT", "SNOMED").
//
// Empty (the default) means no source is configured yet — ReferenceDataService skips it rather
// than treating a blank url as a lookup miss. A url only starts returning rows once Terminology
// actually has the matching ValueSet-*.json loaded; until then GetEncounterCodesAsync comes back
// empty for that system rather than failing the whole screen.
public class EncounterCodeSettings
{
    public const string SectionName = "EncounterCodes";

    public Dictionary<string, string> ValueSetUrls { get; set; } = new();
}
