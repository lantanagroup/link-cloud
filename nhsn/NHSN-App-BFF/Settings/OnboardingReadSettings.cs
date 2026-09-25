namespace LantanaGroup.Link.Nhsn.App.Bff.Settings;

// Deadlines for the GET /onboarding fan-out.
//
// These exist because LinkSdk sets no timeout at all — LinkApiClientBase builds a bare
// FlurlClient, so every call inherits HttpClient's 100-second default. Without a deadline imposed
// here, a service that accepts the connection and then hangs blocks every section for a minute and
// forty seconds, because the response can't be assembled until the slowest one returns.
public class OnboardingReadSettings
{
    public const string SectionName = "OnboardingRead";

    // How long one section may take. Sections run in parallel, so this is also roughly the
    // worst-case wall clock for the whole read.
    public int SectionTimeoutMs { get; set; } = 5000;

    // Backstop on the whole handler, in case a section is added later without its own deadline.
    public int OverallTimeoutMs { get; set; } = 8000;
}
