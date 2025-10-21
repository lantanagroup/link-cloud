using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Tenant.Business.Managers;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Tenant;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class VendorManagerTests : IDisposable
{
    private readonly IServiceScope _scope;
    private readonly IVendorManager _vendorManager;
    private readonly TenantDbContext _dbContext;
    private readonly Mock<INormalizationServiceClient> _normalizationServiceClient;

    public VendorManagerTests(TenantIntegrationTestFixture fixture)
    {
        _scope = fixture.ServiceProvider.CreateScope();
        var serviceProvider = _scope.ServiceProvider;

        _vendorManager = serviceProvider.GetRequiredService<IVendorManager>();
        _dbContext = serviceProvider.GetRequiredService<TenantDbContext>();
        _normalizationServiceClient = serviceProvider.GetRequiredService<Mock<INormalizationServiceClient>>();
        _normalizationServiceClient.Reset();
    }

    public void Dispose() => _scope.Dispose();

    [Fact]
    public async Task DeleteVendorVersion_WhenReferencedByNormalization_ThrowsAndRetainsVersion()
    {
        var vendorVersion = await CreateVendorVersionAsync();
        ConfigureNormalizationReferences(vendorVersion.Id);

        await Assert.ThrowsAsync<VendorVersionInUseException>(() => _vendorManager.DeleteVendorVersionAsync(vendorVersion.Id));

        Assert.NotNull(await _dbContext.VendorVersions.FindAsync(vendorVersion.Id));
    }

    [Fact]
    public async Task DeleteVendor_WhenVersionIsReferencedByNormalization_ThrowsAndRetainsVendor()
    {
        var vendorVersion = await CreateVendorVersionAsync();
        ConfigureNormalizationReferences(vendorVersion.Id);

        await Assert.ThrowsAsync<VendorVersionInUseException>(() => _vendorManager.DeleteVendorAsync(vendorVersion.VendorId));

        Assert.NotNull(await _dbContext.Vendors.FindAsync(vendorVersion.VendorId));
    }

    [Fact]
    public async Task DeleteVendorVersion_WhenNormalizationHasNoReferences_DeletesVersion()
    {
        var vendorVersion = await CreateVendorVersionAsync();
        _normalizationServiceClient
            .Setup(client => client.GetVendorVersionOperationPresetsAsync(vendorVersion.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<List<NormalizationVendorVersionOperationPresetApiModel>>
            {
                StatusCode = StatusCodes.Status200OK,
                Body = []
            });

        await _vendorManager.DeleteVendorVersionAsync(vendorVersion.Id);

        Assert.Null(await _dbContext.VendorVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(version => version.Id == vendorVersion.Id));
    }

    [Fact]
    public async Task CreateVendor_WithAuthentication_PersistsSigningKeySecretId()
    {
        var created = await _vendorManager.CreateVendorAsync(new VendorModel
        {
            Name = $"Vendor-{Guid.NewGuid():N}",
            Authentication = new VendorAuthenticationSettings { SigningKeySecretId = "epic-signing-key" }
        });

        var persisted = await _dbContext.Vendors
            .AsNoTracking()
            .SingleAsync(v => v.Id == created.Id);

        Assert.Equal("epic-signing-key", persisted.Authentication?.SigningKeySecretId);
    }

    [Fact]
    public async Task UpdateVendor_WithAuthentication_PersistsSigningKeySecretId()
    {
        var vendor = await CreateVendorAsync();

        await _vendorManager.UpdateVendorAsync(vendor.Id, new VendorModel
        {
            Id = vendor.Id,
            Name = vendor.Name,
            Authentication = new VendorAuthenticationSettings { SigningKeySecretId = "epic-signing-key" }
        });

        var persisted = await _dbContext.Vendors
            .AsNoTracking()
            .SingleAsync(v => v.Id == vendor.Id);

        Assert.Equal("epic-signing-key", persisted.Authentication?.SigningKeySecretId);
    }

    [Fact]
    public async Task UpdateVendor_WithoutAuthentication_LeavesTheConfiguredSecretIdIntact()
    {
        var vendor = await CreateVendorAsync();
        await _vendorManager.UpdateVendorAsync(vendor.Id, new VendorModel
        {
            Name = vendor.Name,
            Authentication = new VendorAuthenticationSettings { SigningKeySecretId = "epic-signing-key" }
        });

        await _vendorManager.UpdateVendorAsync(vendor.Id, new VendorModel { Name = "Renamed Vendor" });

        var persisted = await _dbContext.Vendors
            .AsNoTracking()
            .SingleAsync(v => v.Id == vendor.Id);

        Assert.Equal("epic-signing-key", persisted.Authentication?.SigningKeySecretId);
    }

    [Fact]
    public async Task SaveChanges_AfterMutatingAuthenticationInPlace_PersistsTheChange()
    {
        var vendor = await CreateVendorAsync();
        await _vendorManager.UpdateVendorAsync(vendor.Id, new VendorModel
        {
            Name = vendor.Name,
            Authentication = new VendorAuthenticationSettings { SigningKeySecretId = "original-key" }
        });

        var tracked = await _dbContext.Vendors.SingleAsync(v => v.Id == vendor.Id);
        tracked.Authentication!.SigningKeySecretId = "rotated-key";
        await _dbContext.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();
        var persisted = await _dbContext.Vendors
            .AsNoTracking()
            .SingleAsync(v => v.Id == vendor.Id);

        Assert.Equal("rotated-key", persisted.Authentication?.SigningKeySecretId);
    }

    private async Task<Vendor> CreateVendorAsync()
    {
        var vendor = new Vendor
        {
            Id = Guid.NewGuid(),
            Name = $"Vendor-{Guid.NewGuid():N}"
        };

        await _dbContext.Vendors.AddAsync(vendor);
        await _dbContext.SaveChangesAsync();

        return vendor;
    }

    private async Task<VendorVersion> CreateVendorVersionAsync()
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
            Version = "test"
        };

        await _dbContext.Vendors.AddAsync(vendor);
        await _dbContext.VendorVersions.AddAsync(vendorVersion);
        await _dbContext.SaveChangesAsync();

        return vendorVersion;
    }

    private void ConfigureNormalizationReferences(Guid vendorVersionId)
    {
        _normalizationServiceClient
            .Setup(client => client.GetVendorVersionOperationPresetsAsync(vendorVersionId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<List<NormalizationVendorVersionOperationPresetApiModel>>
            {
                StatusCode = StatusCodes.Status200OK,
                Body =
                [
                    new NormalizationVendorVersionOperationPresetApiModel
                    {
                        Id = Guid.NewGuid(),
                        VendorVersionId = vendorVersionId,
                        OperationResourceTypeId = Guid.NewGuid()
                    }
                ]
            });
    }
}