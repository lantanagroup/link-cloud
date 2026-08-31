namespace LantanaGroup.Link.Nhsn.App.Bff.Settings;

// Which contract-pending Link capabilities are backed by a real adapter rather than a fixture.
// A flag selects the adapter, never whether an endpoint exists or what shape it returns. Fixture
// responses carry "simulated": true and obviously synthetic ids, so a lower-environment screenshot
// is never mistaken for real facility data; every flag is false in non-development environments
// until the corresponding spec lands. NHSN-App-BFF does not touch DotNet/LinkSdk, so a missing SDK
// method is the platform team's work, not ours — that is capability-flag territory. The exception
// runs the other way: if the SDK method already exists and only our own gateway hasn't wired it,
// that is our own work and needs no flag.
public class LinkCapabilitiesSettings
{
    public const string SectionName = "LinkCapabilities";

    // /api/data/connectionValidation/$validate exists.
    public bool FhirConnectionProbe { get; set; }

    // Epic — blocked on the ehrPatientLists shape not yet carrying a patient collection.
    public bool PatientListWithNames { get; set; }

    // Cerner — LinkSdk has no sFTP coverage at all.
    public bool SftpFileListing { get; set; }
}
