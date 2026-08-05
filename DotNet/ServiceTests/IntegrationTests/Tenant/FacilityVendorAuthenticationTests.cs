using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Tenant;

/// <summary>
/// Data Acquisition reads the vendor's signing key off the facility response rather than making a
/// second call for the vendor, so the facility projection has to carry it.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class FacilityVendorAuthenticationTests : IDisposable
{
    private readonly IServiceScope _scope;
    private readonly IFacilityQueries _facilityQueries;
    private readonly TenantDbContext _dbContext;

    public FacilityVendorAuthenticationTests(TenantIntegrationTestFixture fixture)
    {
        _scope = fixture.ServiceProvider.CreateScope();
        var serviceProvider = _scope.ServiceProvider;

        _facilityQueries = serviceProvider.GetRequiredService<IFacilityQueries>();
        _dbContext = serviceProvider.GetRequiredService<TenantDbContext>();
    }

    public void Dispose() => _scope.Dispose();

    [Fact]
    public async Task GetAsync_CarriesTheVendorsSigningKeySecretId()
    {
        var facilityId = await CreateFacilityWithVendorAsync(
            new VendorAuthenticationSettings { SigningKeySecretId = "epic-signing-key" });

        var facility = await _facilityQueries.GetAsync(facilityId, null, CancellationToken.None);

        Assert.Equal("epic-signing-key", facility?.Vendor?.Authentication?.SigningKeySecretId);
    }

    [Fact]
    public async Task GetAsync_ReturnsNoSigningKey_WhenTheVendorHasNoneConfigured()
    {
        var facilityId = await CreateFacilityWithVendorAsync(authentication: null);

        var facility = await _facilityQueries.GetAsync(facilityId, null, CancellationToken.None);

        Assert.NotNull(facility?.Vendor);
        Assert.Null(facility!.Vendor!.Authentication?.SigningKeySecretId);
    }

    private async Task<string> CreateFacilityWithVendorAsync(VendorAuthenticationSettings? authentication)
    {
        var vendor = new Vendor
        {
            Id = Guid.NewGuid(),
            Name = $"Vendor-{Guid.NewGuid():N}",
            Authentication = authentication
        };
        var vendorVersion = new VendorVersion
        {
            Id = Guid.NewGuid(),
            VendorId = vendor.Id,
            Version = "default"
        };
        var facility = new Facility
        {
            Id = Guid.NewGuid(),
            FacilityId = $"facility-{Guid.NewGuid():N}",
            FacilityName = "Vendor Authentication Test Facility",
            TimeZone = "America/Chicago",
            VendorVersionId = vendorVersion.Id,
            ScheduledReports = new ScheduledReportModel
            {
                Daily = [],
                Weekly = [],
                Monthly = []
            }
        };

        await _dbContext.Vendors.AddAsync(vendor);
        await _dbContext.VendorVersions.AddAsync(vendorVersion);
        await _dbContext.Facilities.AddAsync(facility);
        await _dbContext.SaveChangesAsync();

        return facility.FacilityId;
    }
}
