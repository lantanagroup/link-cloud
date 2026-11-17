using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Tenant.Business.Managers;
using LantanaGroup.Link.Tenant.Data.Entities;
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
    public async Task DeleteVendorVersion_WhenAFacilityUsesIt_ThrowsAndRetainsVersion()
    {
        var vendorVersion = await CreateVendorVersionAsync();
        await CreateFacilityAsync(vendorVersion.Id);
        ConfigureNoNormalizationReferences(vendorVersion.Id);

        await Assert.ThrowsAsync<VendorVersionInUseException>(() => _vendorManager.DeleteVendorVersionAsync(vendorVersion.Id));

        Assert.NotNull(await _dbContext.VendorVersions.FindAsync(vendorVersion.Id));
    }

    /// <summary>
    /// A soft-deleted facility keeps its VendorVersionId, so the row still holds the foreign key and
    /// the delete would still be refused by the database.
    /// </summary>
    [Fact]
    public async Task DeleteVendorVersion_WhenOnlyASoftDeletedFacilityUsesIt_ThrowsAndRetainsVersion()
    {
        var vendorVersion = await CreateVendorVersionAsync();
        await CreateFacilityAsync(vendorVersion.Id, isDeleted: true);
        ConfigureNoNormalizationReferences(vendorVersion.Id);

        await Assert.ThrowsAsync<VendorVersionInUseException>(() => _vendorManager.DeleteVendorVersionAsync(vendorVersion.Id));

        Assert.NotNull(await _dbContext.VendorVersions.FindAsync(vendorVersion.Id));
    }

    [Fact]
    public async Task DeleteVendorVersion_WhenAFacilityUsesIt_SkipsTheNormalizationCall()
    {
        var vendorVersion = await CreateVendorVersionAsync();
        await CreateFacilityAsync(vendorVersion.Id);
        ConfigureNoNormalizationReferences(vendorVersion.Id);

        await Assert.ThrowsAsync<VendorVersionInUseException>(() => _vendorManager.DeleteVendorVersionAsync(vendorVersion.Id));

        _normalizationServiceClient.Verify(
            client => client.GetVendorVersionOperationPresetsAsync(It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// A count alone leaves an administrator hunting, and a soft-deleted facility cannot be found in
    /// the UI at all - so the refusal names each facility and says which of them is deleted.
    /// </summary>
    [Fact]
    public async Task DeleteVendorVersion_WhenFacilitiesUseIt_NamesThemInTheRefusal()
    {
        var vendorVersion = await CreateVendorVersionAsync();
        var live = await CreateFacilityAsync(vendorVersion.Id);
        var softDeleted = await CreateFacilityAsync(vendorVersion.Id, isDeleted: true);
        ConfigureNoNormalizationReferences(vendorVersion.Id);

        var exception = await Assert.ThrowsAsync<VendorVersionInUseException>(
            () => _vendorManager.DeleteVendorVersionAsync(vendorVersion.Id));

        Assert.Contains("is in use by 2 facility configurations", exception.Message);
        Assert.Contains(live.FacilityId, exception.Message);
        Assert.Contains(softDeleted.FacilityId, exception.Message);
        Assert.Contains($"{softDeleted.FacilityId} ({softDeleted.FacilityName}, deleted)", exception.Message);
    }

    /// <summary>
    /// The refusal reaches an administrator who picked a version off a list by its vendor and version,
    /// so that is what it names them by; the id identifies the row for the log line, not for them.
    /// </summary>
    [Fact]
    public async Task DeleteVendorVersion_WhenAFacilityUsesIt_NamesTheVendorAndVersionRatherThanTheId()
    {
        var vendorVersion = await CreateVendorVersionAsync();
        var vendorName = await VendorNameAsync(vendorVersion.VendorId);
        await CreateFacilityAsync(vendorVersion.Id);
        ConfigureNoNormalizationReferences(vendorVersion.Id);

        var exception = await Assert.ThrowsAsync<VendorVersionInUseException>(
            () => _vendorManager.DeleteVendorVersionAsync(vendorVersion.Id));

        Assert.StartsWith(
            $"Version '{vendorVersion.Version}' of vendor '{vendorName}' is in use by 1 facility configuration",
            exception.Message);
        Assert.DoesNotContain(vendorVersion.Id.ToString(), exception.Message);
        Assert.DoesNotContain(vendorVersion.VendorId.ToString(), exception.Message);
    }

    /// <summary>
    /// A vendor delete is refused by the same foreign key, so its message has to be about the vendor
    /// the caller asked to delete - naming the facilities, and which version each one holds.
    /// </summary>
    [Fact]
    public async Task DeleteVendor_WhenAFacilityUsesOneOfItsVersions_NamesTheFacilityAndVersion()
    {
        var vendorVersion = await CreateVendorVersionAsync();
        var vendorName = await VendorNameAsync(vendorVersion.VendorId);
        var facility = await CreateFacilityAsync(vendorVersion.Id);
        ConfigureNoNormalizationReferences(vendorVersion.Id);

        var exception = await Assert.ThrowsAsync<VendorVersionInUseException>(
            () => _vendorManager.DeleteVendorAsync(vendorVersion.VendorId));

        //the vendor by name: the id is in the log line, and means nothing to whoever reads this.
        Assert.Contains($"Vendor '{vendorName}' is in use by 1 facility configuration", exception.Message);
        Assert.Contains($"{facility.FacilityId} ({facility.FacilityName}, version {vendorVersion.Version})", exception.Message);
        Assert.DoesNotContain(vendorVersion.Id.ToString(), exception.Message);
        Assert.DoesNotContain(vendorVersion.VendorId.ToString(), exception.Message);
    }

    /// <summary>
    /// Deleting the vendor cascades to its versions, so a facility holding one of them fails the same
    /// foreign key.
    /// </summary>
    [Fact]
    public async Task DeleteVendor_WhenAFacilityUsesOneOfItsVersions_ThrowsAndRetainsVendor()
    {
        var vendorVersion = await CreateVendorVersionAsync();
        await CreateFacilityAsync(vendorVersion.Id);
        ConfigureNoNormalizationReferences(vendorVersion.Id);

        await Assert.ThrowsAsync<VendorVersionInUseException>(() => _vendorManager.DeleteVendorAsync(vendorVersion.VendorId));

        Assert.NotNull(await _dbContext.Vendors.FindAsync(vendorVersion.VendorId));
        Assert.NotNull(await _dbContext.VendorVersions.FindAsync(vendorVersion.Id));
    }

    /// <summary>
    /// The pre-checks cannot close the window between themselves and the delete: here the facility
    /// is assigned while Normalization is being asked, so the database is what refuses the delete.
    /// It still has to reach the caller as a conflict rather than a 500.
    /// </summary>
    [Fact]
    public async Task DeleteVendorVersion_WhenAFacilityIsAssignedAfterTheChecks_ThrowsAndRetainsVersion()
    {
        var vendorVersion = await CreateVendorVersionAsync();

        _normalizationServiceClient
            .Setup(client => client.GetVendorVersionOperationPresetsAsync(vendorVersion.Id, null, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await CreateFacilityAsync(vendorVersion.Id);
                return new LinkApiResponse<List<NormalizationVendorVersionOperationPresetApiModel>>
                {
                    StatusCode = StatusCodes.Status200OK,
                    Body = []
                };
            });

        var exception = await Assert.ThrowsAsync<VendorVersionInUseException>(
            () => _vendorManager.DeleteVendorVersionAsync(vendorVersion.Id));

        Assert.Contains("still referenced", exception.Message);
        Assert.NotNull(await _dbContext.VendorVersions.AsNoTracking()
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

    private async Task<string> VendorNameAsync(Guid vendorId) =>
        await _dbContext.Vendors
            .AsNoTracking()
            .Where(vendor => vendor.Id == vendorId)
            .Select(vendor => vendor.Name)
            .SingleAsync();

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

    private async Task<Facility> CreateFacilityAsync(Guid vendorVersionId, bool isDeleted = false)
    {
        var facility = new Facility
        {
            Id = Guid.NewGuid(),
            FacilityId = $"facility-{Guid.NewGuid():N}",
            FacilityName = "Vendor Version Delete Test Facility",
            TimeZone = "America/Chicago",
            IsDeleted = isDeleted,
            VendorVersionId = vendorVersionId,
            ScheduledReports = new ScheduledReportModel
            {
                Daily = [],
                Weekly = [],
                Monthly = []
            }
        };

        await _dbContext.Facilities.AddAsync(facility);
        await _dbContext.SaveChangesAsync();

        return facility;
    }

    private void ConfigureNoNormalizationReferences(Guid vendorVersionId)
    {
        _normalizationServiceClient
            .Setup(client => client.GetVendorVersionOperationPresetsAsync(vendorVersionId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<List<NormalizationVendorVersionOperationPresetApiModel>>
            {
                StatusCode = StatusCodes.Status200OK,
                Body = []
            });
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