namespace LantanaGroup.Link.Nhsn.App.Bff.Settings;

// Link capabilities the frontend can switch on or off. Each is reported to the frontend through
// UserInfoResponse.Capabilities; the adapters behind them all call Data Acquisition for real, so a
// flag controls whether the UI offers the feature, not whether its data is genuine.
public class LinkCapabilitiesSettings
{
    public const string SectionName = "LinkCapabilities";

    // /api/data/connectionValidation/$validate exists. The probe is always the real one now; this
    // is reported to the frontend only.
    public bool FhirConnectionProbe { get; set; }

    // Epic — live through GET fhirQueryList?includePatients=true. While false, hides the Census
    // step's fetch/view UI (and the Report step's Previous/New Pull tabs).
    public bool PatientListWithNames { get; set; }

    // Cerner — live through the ad-hoc sFTP test-connection preview. Same UI-hiding behavior as
    // PatientListWithNames.
    public bool SftpFileListing { get; set; }

    // MRN Identifier Intake's rule builder — no LinkSdk client exposes a report patient's real
    // Patient.identifier array yet. See IPatientIdentifierGateway's doc comment.
    public bool PatientIdentifierLookup { get; set; }

    // Not a real-vs-fixture adapter flag like the others above — a UX toggle. When true, a facility
    // whose onboarding already completed keeps an "Onboarding" item in the main navigation and can
    // freely revisit/edit every step; when false (the default), onboarding is a one-way door and
    // the wizard is unreachable once complete. Reuses this settings class purely for its existing
    // appsettings -> UserInfoResponse.Capabilities -> frontend plumbing, not because this fits the
    // "real vs fixture" theme.
    public bool OnboardingRevisit { get; set; }
}
