namespace LantanaGroup.Link.Onboarding.Mcp;

public sealed class OnboardingOptions
{
    public string TenantApiBaseUrl { get; set; } = "";
    public string MeasureEvalApiBaseUrl { get; set; } = "http://localhost:8067/api/measureeval/";
    // Optional local-only credential. Never included in tool output.
    public string? BearerToken { get; set; }
    public string? MeasureEvalBearerToken { get; set; }
    public string DataAcquisitionApiBaseUrl { get; set; } = "http://localhost:8065/api/data/";
    public string? DataAcquisitionBearerToken { get; set; }
    public ProfileOption[] ConnectionProfiles { get; set; } = [];
    public ProfileOption[] QueryPlanProfiles { get; set; } = [];
}

public sealed record ProfileOption(string Id, string DisplayName);
public sealed record MeasureDefinitionOption(string Id, long Version);
public sealed record VendorOption(Guid Id, string Name);
public sealed record VendorVersionOption(Guid Id, Guid VendorId, string Version);
public sealed record SourceResult<T>(string Status, string Source, T[] Options, string? Message = null);
public sealed record OnboardingOptionsResult(
    string Status,
    SourceResult<ProfileOption> ConnectionProfiles,
    SourceResult<ProfileOption> QueryPlanProfiles,
    SourceResult<MeasureDefinitionOption> MeasureDefinitions,
    SourceResult<VendorOption> Vendors,
    SourceResult<VendorVersionOption> VendorVersions);
