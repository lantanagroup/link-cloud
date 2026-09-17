using Automation.UI.Services.ApiHealth.TestSuites;
using FluentAssertions;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class ApiEndPointLibraryTests
{
    [Fact]
    public void VendorSteps_AreRegisteredForTenantOnly()
    {
        var tenantEndpoints = ApiEndPointLibrary.GetServiceEndpoints(ApiEndPointLibrary.ServiceNames.Tenant);
        var normalizationEndpoints = ApiEndPointLibrary.GetServiceEndpoints(ApiEndPointLibrary.ServiceNames.Normalization);

        tenantEndpoints.Select(endpoint => endpoint.EndpointName).Should().Contain(
        [
            ApiEndPointLibrary.TenantSteps.VendorPost201,
            ApiEndPointLibrary.TenantSteps.VendorPost409,
            ApiEndPointLibrary.TenantSteps.VendorGet200,
            ApiEndPointLibrary.TenantSteps.VendorsGet200,
            ApiEndPointLibrary.TenantSteps.VendorDelete204
        ]);

        normalizationEndpoints.Select(endpoint => endpoint.EndpointName).Should().NotContain(
        [
            ApiEndPointLibrary.TenantSteps.VendorPost201,
            ApiEndPointLibrary.TenantSteps.VendorPost409,
            ApiEndPointLibrary.TenantSteps.VendorGet200,
            ApiEndPointLibrary.TenantSteps.VendorsGet200,
            ApiEndPointLibrary.TenantSteps.VendorDelete204
        ]);
    }
}