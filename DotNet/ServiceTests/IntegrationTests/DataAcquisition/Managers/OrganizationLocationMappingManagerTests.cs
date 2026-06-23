using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Managers;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class OrganizationLocationMappingManagerTests
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public OrganizationLocationMappingManagerTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private IOrganizationLocationMappingManager CreateManager(IServiceScope scope)
    {
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        return new OrganizationLocationMappingManager(database, dbContext);
    }

    private static string NewFacilityId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
    private static string NewLocationId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    [Fact]
    public async Task CreateAsync_ValidModel_ReturnsModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var facilityId = NewFacilityId("Facility-ABC");
        var locationId = NewLocationId("Location-123");

        var createModel = new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = locationId,
            LocationName = "Main Hospital Bed 101",
            LocationAlias = "ICU-Bed-101",
            PartOfValue = "Location-Parent-001",
            PartOfId = null,
            IsOrgLocation = true,
            IsActive = true
        };

        var result = await manager.CreateAsync(createModel);

        Assert.NotNull(result);
        Assert.Equal(facilityId, result.FacilityId);
        Assert.Equal(locationId, result.LocationId);
        Assert.Equal("Main Hospital Bed 101", result.LocationName);
        Assert.True(result.IsOrgLocation);
    }

    [Fact]
    public async Task UpdateByIdAsync_ValidUpdate_ReturnsUpdatedModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = NewFacilityId("Test-Fac"),
            LocationId = NewLocationId("Loc-001"),
            IsOrgLocation = false
        });

        var updateModel = new UpdateOrganizationLocationMappingModel
        {
            LocationName = "Updated Name",
            IsOrgLocation = true,
            IsActive = false
        };

        var result = await manager.UpdateByIdAsync(created.LocationMappingId, updateModel);

        Assert.Equal("Updated Name", result.LocationName);
        Assert.True(result.IsOrgLocation);
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task UpdateByFacilityIdAndLocationIdAsync_ValidUpdate_ReturnsUpdatedModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var facilityId = NewFacilityId("Fac-XYZ");
        var locationId = NewLocationId("Loc-555");
        await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = locationId,
            LocationName = "Old Name"
        });

        var updateModel = new UpdateOrganizationLocationMappingModel { LocationName = "New Name" };

        var result = await manager.UpdateByFacilityIdAndLocationIdAsync(facilityId, locationId, updateModel);

        Assert.Equal("New Name", result.LocationName);
    }

    [Fact]
    public async Task DeleteByIdAsync_Existing_DeletesRecord()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = NewFacilityId("Delete-Test"),
            LocationId = NewLocationId("Loc-999")
        });

        await manager.DeleteByIdAsync(created.LocationMappingId);

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => queries.GetByIdAsync(created.LocationMappingId));
    }

    [Fact]
    public async Task DeleteByFacilityIdAndLocationIdAsync_Existing_DeletesRecord()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var facilityId = NewFacilityId("Fac-Delete");
        var locationId = NewLocationId("Loc-Delete");
        await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = locationId
        });

        await manager.DeleteByFacilityIdAndLocationIdAsync(facilityId, locationId);

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        Assert.Null(await queries.GetByFacilityIdAndLocationIdAsync(facilityId, locationId));
    }

    [Fact]
    public async Task UpdateByIdAsync_NotFound_ThrowsKeyNotFoundException()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            manager.UpdateByIdAsync(99999, new UpdateOrganizationLocationMappingModel()));
    }
}
