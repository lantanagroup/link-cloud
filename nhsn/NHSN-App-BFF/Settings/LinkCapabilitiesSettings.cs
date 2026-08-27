namespace LantanaGroup.Link.Nhsn.App.Bff.Settings;

// Which contract-pending Link capabilities are backed by a real adapter rather than a fixture.
// A flag selects the adapter, never whether an endpoint exists or what shape it returns. Fixture
// responses carry "simulated": true and obviously synthetic ids, so a lower-environment screenshot
// is never mistaken for real facility data; every flag is false in non-development environments
// until the corresponding spec lands. A missing LinkSdk method is not a capability — there's
// nothing to fall back to and nobody to wait for, so an unmerged SDK PR is just unfinished work.
public class LinkCapabilitiesSettings
{
    public const string SectionName = "LinkCapabilities";

    // /api/data/connectionValidation/$validate exists.
    public bool FhirConnectionProbe { get; set; }

    // Epic — blocked on the ehrPatientLists shape not yet carrying a patient collection.
    public bool PatientListWithNames { get; set; }
}
