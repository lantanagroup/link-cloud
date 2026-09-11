using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;

namespace LantanaGroup.Link.Nhsn.App.Bff.Domain.VendorProfiles;

// Everything that differs between Epic and Cerner, expressed as data.
//
// Vendor is a policy, not a branch. The UI branches on this object, so no step component contains
// a vendor name, and adding a third vendor is a profile plus a query-plan template rather than a
// UI release.
//
// Census acquisition is determined by vendor and never asked — Cerner uses sFTP, Epic uses patient
// lists. There's no usesAdt question and no user-facing choice.
public sealed record VendorProfile
{
    public required EhrVendor Vendor { get; init; }

    public required string DisplayName { get; init; }

    public required CensusAcquisition CensusAcquisition { get; init; }

    // Which patient-list slots this vendor expects. Empty when it acquires by sFTP.
    public IReadOnlyList<string> PatientListKeys { get; init; } = [];

    public IReadOnlyList<string> LocationMethods { get; init; } = [];

    // Keys for GET /documents/{documentKey} — never filenames. The document provider resolves
    // them against a fixed allow-list and never uses them to build a file path.
    public required VendorDocumentKeys DocumentKeys { get; init; }

    // Column label for the vendor's own location code in the HSLOC mapping table.
    public required string HslocSourceLabel { get; init; }
}

public sealed record VendorDocumentKeys
{
    public string? CensusInstructions { get; init; }
    public string? JwksInstructions { get; init; }
    public string? LocationOrgResolution { get; init; }
}

// How a vendor's patient census reaches Link. Server-side policy, not user input.
public enum CensusAcquisition
{
    // Epic — six FHIR List resources, one per admit/discharge and age-band slot.
    PatientList,

    // Cerner — files collected over sFTP.
    Sftp
}
