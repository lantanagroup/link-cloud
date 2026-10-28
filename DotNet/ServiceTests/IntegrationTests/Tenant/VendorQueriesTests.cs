using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Tenant.Business.Managers;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Tenant;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class VendorQueriesTests : IDisposable
{
    private readonly IServiceScope _scope;
    private readonly IVendorQueries _vendorQueries;
    private readonly IVendorManager _vendorManager;
    private readonly TenantDbContext _dbContext;

    public VendorQueriesTests(TenantIntegrationTestFixture fixture)
    {
        _scope = fixture.ServiceProvider.CreateScope();
        var serviceProvider = _scope.ServiceProvider;

        _vendorQueries = serviceProvider.GetRequiredService<IVendorQueries>();
        _vendorManager = serviceProvider.GetRequiredService<IVendorManager>();
        _dbContext = serviceProvider.GetRequiredService<TenantDbContext>();
    }

    public void Dispose() => _scope.Dispose();

    [Fact]
    public async Task GetVendor_ReturnsTheConfiguredSigningKeySecretId()
    {
        var vendorVersion = await CreateVendorWithSigningKeyAsync("epic-signing-key");

        var vendor = await _vendorQueries.GetVendor(vendorVersion.VendorId);

        Assert.Equal("epic-signing-key", vendor?.Authentication?.SigningKeySecretId);
    }

    [Fact]
    public async Task GetVendorVersion_ExposesTheParentVendorsSigningKeySecretId()
    {
        var vendorVersion = await CreateVendorWithSigningKeyAsync("cerner-signing-key");

        var version = await _vendorQueries.GetVendorVersion(vendorVersion.Id);

        Assert.Equal("cerner-signing-key", version?.Authentication?.SigningKeySecretId);
    }

    private async Task<VendorVersion> CreateVendorWithSigningKeyAsync(string signingKeySecretId)
    {
        var vendor = new Vendor
        {
            Id = Guid.NewGuid(),
            Name = $"Vendor-{Guid.NewGuid():N}"
        };
        var vendorVersion = new VendorVersion
        {
            Id = Guid.NewGuid(),
            VendorId = vendor.Id,
            Version = "default"
        };

        await _dbContext.Vendors.AddAsync(vendor);
        await _dbContext.VendorVersions.AddAsync(vendorVersion);
        await _dbContext.SaveChangesAsync();

        await _vendorManager.UpdateVendorAsync(vendor.Id, new VendorModel
        {
            Name = vendor.Name,
            Authentication = new VendorAuthenticationSettings { SigningKeySecretId = signingKeySecretId }
        });

        return vendorVersion;
    }
}
