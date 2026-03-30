using LantanaGroup.Link.Automation.Helpers;

namespace LantanaGroup.Link.Tests.E2ETests;

/// <summary>
/// Thin convenience layer that delegates to <see cref="FacilitySetupHelper"/>
/// using the resolved SDK clients from <see cref="TestServices"/>.
/// </summary>
internal static class SdkSetupHelper
{
    public static Task EnsureFacilityAsync(TestServices b, string facilityId, string? measureId) =>
        FacilitySetupHelper.EnsureFacilityAsync(b.FacilityClient, b.Output, facilityId, measureId);

    public static Task EnsureNormalizationConfigAsync(TestServices b, string facilityId) =>
        FacilitySetupHelper.EnsureNormalizationConfigAsync(b.NormalizationClient, b.Output, facilityId);

    public static Task EnsureQueryPlansAsync(TestServices b, string facilityId, string? measureId, string ehrDescription) =>
        FacilitySetupHelper.EnsureQueryPlansAsync(b.DataAcquisitionClient, b.Output, facilityId, measureId, ehrDescription);

    public static Task EnsureQueryConfigAsync(TestServices b, string facilityId) =>
        FacilitySetupHelper.EnsureQueryConfigAsync(b.DataAcquisitionClient, b.AutomationCfg, b.Output, facilityId);

    public static Task CleanupFacilityAsync(TestServices b, string facilityId)
    {
        b.Output.WriteLine("Cleaning up...");
        return FacilitySetupHelper.CleanupFacilityAsync(b.FacilityClient, b.NormalizationClient, b.DataAcquisitionClient, b.Output, facilityId);
    }
}
