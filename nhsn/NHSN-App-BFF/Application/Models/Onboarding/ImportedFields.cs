namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

// Mirrors ImportedFields in NHSN-App-UI/src/core/api/contracts.ts. Every slice is optional - a
// sheet that leaves a whole section blank simply omits it - and every property name matches the
// UI's own *Draft shape so the frontend can patch a section straight from what it receives here.
public sealed record ImportedFields
{
    public ImportedFhir? Fhir { get; init; }
    public ImportedCensus? Census { get; init; }
    public ImportedLocationOrg? LocationOrg { get; init; }
    public ImportedHsloc? Hsloc { get; init; }
    public ImportedEncounter? Encounter { get; init; }
}

public sealed record ImportedFhir
{
    public string? FhirServerBaseUrl { get; init; }
    public int? MaxConcurrentRequests { get; init; }
    public int? MaxRetries { get; init; }
    public string? MinAcquisitionPullTime { get; init; }
    public string? MaxAcquisitionPullTime { get; init; }
    public string? LagDuration { get; init; }
}

public sealed record ImportedCensus
{
    public Dictionary<string, string>? PatientListIds { get; init; }
    public string? SftpHost { get; init; }
    public int? SftpPort { get; init; }
    public string? SftpRemoteDirectory { get; init; }
    public bool? SftpRemoveAfterProcessing { get; init; }
    public string? AcquisitionFrequency { get; init; }

    // Secrets, parsed off the sheet only long enough to forward to
    // IPatientsOfInterestService.SaveSftpCredentialsAsync (ManualUploadTemplateService /
    // OnboardingWriteService.SaveImportedFieldsAsync). Never included in the ImportResult sent
    // back to the browser, and never persisted to the draft - see HasCredentials below.
    public string? SftpUsername { get; init; }
    public string? SftpPassword { get; init; }

    // Set on the response (never on what's parsed from the sheet) once SftpUsername/SftpPassword
    // have been saved, so the frontend can show "credentials are on file" without the secrets
    // themselves ever round-tripping back to the browser.
    public bool? HasCredentials { get; init; }
}

public sealed record ImportedLocationOrg
{
    public string? Method { get; init; }
    public List<string>? ManagingOrganizationIds { get; init; }
    public List<ImportedLocationType>? LocationTypes { get; init; }
    public List<ImportedLocationIdentifier>? LocationIdentifiers { get; init; }
    public string? CustomFhirPath { get; init; }
}

public sealed record ImportedLocationType
{
    public required string Code { get; init; }
    public required string Alias { get; init; }
}

public sealed record ImportedLocationIdentifier
{
    public required string System { get; init; }
    public required string Code { get; init; }
}

public sealed record ImportedHsloc
{
    public List<ImportedHslocMapping>? Mappings { get; init; }
}

public sealed record ImportedHslocMapping
{
    public required string SourceCode { get; init; }
    public string? SourceDisplay { get; init; }
    public required string HslocCode { get; init; }
}

public sealed record ImportedEncounter
{
    public List<ImportedEncounterMapping>? Mappings { get; init; }
}

public sealed record ImportedEncounterMapping
{
    public required string System { get; init; }
    public required string Code { get; init; }
    public string? Display { get; init; }
    public required string EncounterType { get; init; }
}
