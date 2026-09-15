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

    [Fact]
    public void FacilityLocationAndHslocMappingSteps_AreRegisteredForNormalization()
    {
        var normalizationEndpoints = ApiEndPointLibrary.GetServiceEndpoints(ApiEndPointLibrary.ServiceNames.Normalization)
            .Select(endpoint => endpoint.EndpointName);

        normalizationEndpoints.Should().Contain(
        [
            ApiEndPointLibrary.NormalizationSteps.LocationPost201,
            ApiEndPointLibrary.NormalizationSteps.LocationPost400EmptyLocationId,
            ApiEndPointLibrary.NormalizationSteps.LocationPost409Duplicate,
            ApiEndPointLibrary.NormalizationSteps.LocationGet200,
            ApiEndPointLibrary.NormalizationSteps.LocationGet400EmptyLocationId,
            ApiEndPointLibrary.NormalizationSteps.LocationGet404,
            ApiEndPointLibrary.NormalizationSteps.MappingPost201,
            ApiEndPointLibrary.NormalizationSteps.MappingPost400EmptyLocalCode,
            ApiEndPointLibrary.NormalizationSteps.MappingPost404UnknownLocation,
            ApiEndPointLibrary.NormalizationSteps.MappingPost409Duplicate,
            ApiEndPointLibrary.NormalizationSteps.MappingGet200,
            ApiEndPointLibrary.NormalizationSteps.MappingGet400EmptyId,
            ApiEndPointLibrary.NormalizationSteps.MappingGet404,
            ApiEndPointLibrary.NormalizationSteps.MappingSearch200HasResults,
            ApiEndPointLibrary.NormalizationSteps.MappingSearch200Empty,
            ApiEndPointLibrary.NormalizationSteps.MappingPut202,
            ApiEndPointLibrary.NormalizationSteps.MappingPut400EmptyLocalCode,
            ApiEndPointLibrary.NormalizationSteps.MappingPut404,
            ApiEndPointLibrary.NormalizationSteps.MappingPut409Duplicate,
            ApiEndPointLibrary.NormalizationSteps.MappingDelete204,
            ApiEndPointLibrary.NormalizationSteps.MappingDelete400EmptyId,
            ApiEndPointLibrary.NormalizationSteps.MappingDeleteFacility204,
            ApiEndPointLibrary.NormalizationSteps.MappingDeleteFacility400EmptyFacility
        ]);
    }
}