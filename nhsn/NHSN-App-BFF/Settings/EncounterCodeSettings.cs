namespace LantanaGroup.Link.Nhsn.App.Bff.Settings;

// Canonical CodeSystem urls the Encounter Mapping step's CPT/SNOMED reference list is searched
// from (Terminology's `terminology/codes?codeSystem=` endpoint), keyed by the label shown in the
// UI (e.g. "CPT", "SNOMED"). These are fixed FHIR-standard identifiers rather than
// environment-specific values, so they're defaulted in appsettings.json.
//
// Empty (the default) means no source is configured yet — ReferenceDataService skips it rather
// than treating a blank url as a lookup miss. A url only starts returning rows once Terminology
// actually has a matching CodeSystem loaded; until then GetEncounterCodesAsync comes back empty
// for that system rather than failing the whole screen.
public class EncounterCodeSettings
{
    public const string SectionName = "EncounterCodes";

    public Dictionary<string, string> CodeSystemUrls { get; set; } = new();
}
