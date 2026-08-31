using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;

namespace LantanaGroup.Link.Nhsn.App.Bff.Domain.VendorProfiles;

// The two vendor profiles in scope.
//
// Code rather than seed data, deliberately: a profile is policy, and policy that ships with the
// build cannot drift between environments the way a seeded row can. The per-vendor query-plan
// templates are the opposite case and are seeded, since their content changes without a code
// change.
//
// Other is out of scope. The generic-OAuth fields the POC carried for it — otherTokenUrl,
// otherClientId, otherClientSecret, otherScope — are deliberately not implemented anywhere.
public static class VendorProfileCatalog
{
    // The six patient-list slots Epic uses: a 2x3 grid of admit/discharge against age band. Six
    // exist because Epic caps a query at roughly 1500 patients.
    private static readonly string[] EpicPatientListKeys =
    [
        "admit-lt-24",
        "admit-24-to-48",
        "admit-gt-48",
        "discharge-lt-24",
        "discharge-24-to-48",
        "discharge-gt-48"
    ];

    private static readonly VendorProfile Epic = new()
    {
        Vendor = EhrVendor.Epic,
        DisplayName = "Epic",
        CensusAcquisition = CensusAcquisition.PatientList,
        PatientListKeys = EpicPatientListKeys,
        LocationMethods = ["managing-org", "location-identifier", "custom-fhir-path"],
        DocumentKeys = new VendorDocumentKeys
        {
            CensusInstructions = "epic-census-instructions",
            JwksInstructions = "epic-jwks-instructions",
            LocationOrgResolution = "location-org-resolution"
        },
        HslocSourceLabel = "Epic location"
    };

    private static readonly VendorProfile Cerner = new()
    {
        Vendor = EhrVendor.Cerner,
        DisplayName = "Cerner",
        CensusAcquisition = CensusAcquisition.Sftp,

        // Empty, and that is the point: Cerner acquires by sFTP, so the six list inputs are not
        // rendered at all rather than rendered and ignored.
        PatientListKeys = [],

        // Cerner adds location-type, backed by its "Site" location search.
        LocationMethods = ["managing-org", "location-identifier", "location-type", "custom-fhir-path"],
        DocumentKeys = new VendorDocumentKeys
        {
            CensusInstructions = "cerner-census-instructions",
            JwksInstructions = "cerner-jwks-instructions",
            LocationOrgResolution = "location-org-resolution"
        },
        HslocSourceLabel = "Cerner location"
    };

    public static IReadOnlyList<VendorProfile> All { get; } = [Epic, Cerner];

    public static VendorProfile? Find(EhrVendor vendor) =>
        All.FirstOrDefault(profile => profile.Vendor == vendor);
}
