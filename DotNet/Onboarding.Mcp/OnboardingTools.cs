using System.ComponentModel;
using ModelContextProtocol.Server;

namespace LantanaGroup.Link.Onboarding.Mcp;

[McpServerToolType]
public sealed class OnboardingTools(OnboardingOptionsService service, CreateFacilityService facilityService,
    DataAcquisitionConfigurationService acquisitionService, EpicEndpointDirectory epicDirectory)
{
    [McpServerTool(Name = "lookup_epic_fhir_endpoints", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Find published Epic R4 FHIR base URL candidates by facility/health-system name and optional city/state words in the cached public Epic directory. Returns paged matches and source timestamp; available with empty records means no match. Stale or unavailable results are explicit. Does not save settings or test connectivity. Confirm ambiguous matches and intended integration; patient-access endpoints do not establish backend-service access.")]
    public EpicEndpointLookupResult LookupEpicFhirEndpoints(
        [Description("Facility or health-system name, optionally including city/state; 2–200 characters. All words must match.")] string search,
        int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default) =>
        epicDirectory.Lookup(search, pageNumber, pageSize, cancellationToken);

    [McpServerTool(Name = "get_data_acquisition_configuration", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Get a facility's current FHIR data acquisition connection settings without making changes. Returns available, not_found, or unavailable. Authentication details are excluded. Pull times are facility-local; this does not test connectivity or retrieve query plans.")]
    public Task<GetDataAcquisitionConfigurationResult> GetDataAcquisitionConfiguration(
        [Description("Facility identifier.")] string facilityId,
        CancellationToken cancellationToken = default) => acquisitionService.GetAsync(facilityId, cancellationToken);

    [McpServerTool(Name = "validate_fhir_server_connection", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Test a FHIR base URL using DataAcquisition's ValidateFhirServerConnection without saving configuration. Performs an unauthenticated metadata/CapabilityStatement check; does not verify authenticated patient-data access. Returns connected, failed, or unavailable.")]
    public Task<ConnectionValidationResult> ValidateFhirServerConnection(
        [Description("Absolute HTTP(S) FHIR base URL without credentials, query, or fragment.")] string fhirServerBaseUrl,
        CancellationToken cancellationToken = default) =>
        acquisitionService.ValidateConnectionAsync(fhirServerBaseUrl, cancellationToken);

    [McpServerTool(Name = "save_data_acquisition_configuration", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true)]
    [Description("Add or update a facility's FHIR data acquisition connection settings only when explicitly requested. Reads existing settings, then POSTs if absent or PUTs if present. Preserves omitted fields and existing authentication. Does not configure credentials or query plans. After a successful save with a supplied FHIR URL, calls ValidateFhirServerConnection and returns its unauthenticated metadata check result separately from the save status. Pull times are facility-local HH:mm:ss. Never automatically retry an unknown write outcome.")]
    public Task<SaveDataAcquisitionResult> SaveDataAcquisitionConfiguration(
        [Description("Existing facility identifier.")] string facilityId,
        [Description("FHIR HTTP(S) base URL without credentials. Required for a new configuration; empty preserves an existing URL.")] string fhirServerBaseUrl = "",
        [Description("Positive concurrency limit. -1 preserves existing value; new configurations default to 1.")] int maxConcurrentRequests = -1,
        [Description("Retry limit 0 through 10. -1 preserves existing value or service default.")] int maxRetries = -1,
        [Description("Facility-local earliest pull time HH:mm:ss; empty preserves/defaults, clear removes limit.")] string minAcquisitionPullTime = "",
        [Description("Facility-local latest pull time HH:mm:ss; empty preserves/defaults, clear removes limit.")] string maxAcquisitionPullTime = "",
        CancellationToken cancellationToken = default) => acquisitionService.SaveAsync(facilityId, fhirServerBaseUrl,
            maxConcurrentRequests, maxRetries, minAcquisitionPullTime, maxAcquisitionPullTime, cancellationToken);

    [McpServerTool(Name = "get_onboarding_options", ReadOnly = true, Destructive = false, UseStructuredContent = true)]
    [Description("List configured Link Cloud connection/query-plan profiles, live Tenant vendor/version choices, and live measure definitions from MeasureEval. No changes are made. Unavailable sources are explicit; only profiles are operator-maintained catalogs.")]
    public Task<OnboardingOptionsResult> GetOnboardingOptions(
        CancellationToken cancellationToken = default) => service.GetOnboardingOptions(cancellationToken);

    [McpServerTool(Name = "create_facility", ReadOnly = false, Destructive = false, Idempotent = false, UseStructuredContent = true)]
    [Description("Create a Tenant facility only when the user explicitly requests creation. First collect the ID, name, IANA time zone, and optional existing vendor-version UUID. Existing facilities are never updated. Sends empty reporting schedules; Tenant owns DMRP schedule derivation. Does not apply connection/query-plan profiles or enroll measures. Never automatically retry an unknown outcome; check Tenant first.")]
    public Task<CreateFacilityResult> CreateFacility(
        [Description("Facility identifier. Letters, digits, and hyphens only; Tenant may require a numeric identifier.")] string facilityId,
        [Description("Facility display name in plain text.")] string facilityName,
        [Description("IANA time zone ID, for example America/New_York.")] string timeZone,
        [Description("Existing vendor-version UUID from get_onboarding_options. Empty string to omit the vendor association.")] string vendorVersionId = "",
        CancellationToken cancellationToken = default) =>
        facilityService.CreateAsync(facilityId, facilityName, timeZone, vendorVersionId, cancellationToken);
}
