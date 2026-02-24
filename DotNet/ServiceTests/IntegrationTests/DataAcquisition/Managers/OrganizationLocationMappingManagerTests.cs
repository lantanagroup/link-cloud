using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Queries;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Managers;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class OrganizationLocationMappingManagerTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public OrganizationLocationMappingManagerTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private IOrganizationLocationMappingManager CreateManager(IServiceScope scope)
    {
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        return new OrganizationLocationMappingManager(database);
    }

    [Fact]
    public async Task CreateAsync_ValidModel_ReturnsModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);

        var createModel = new CreateOrganizationLocationMappingModel
        {
            FacilityId = "Facility-ABC",
            LocationId = "Location-123",
            LocationName = "Main Hospital Bed 101",
            LocationAlias = "ICU-Bed-101",
            PartOfValue = "Location-Parent-001",
            PartOfId = null,
            IsOrgLocation = true,
            IsActive = true
        };

        var result = await manager.CreateAsync(createModel);

        Assert.NotNull(result);
        Assert.Equal("Facility-ABC", result.FacilityId);
        Assert.Equal("Location-123", result.LocationId);
        Assert.Equal("Main Hospital Bed 101", result.LocationName);
        Assert.True(result.IsOrgLocation);
    }

    [Fact]
    public async Task UpdateByIdAsync_ValidUpdate_ReturnsUpdatedModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = "Test-Fac",
            LocationId = "Loc-001",
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
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);
        await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = "Fac-XYZ",
            LocationId = "Loc-555",
            LocationName = "Old Name"
        });

        var updateModel = new UpdateOrganizationLocationMappingModel { LocationName = "New Name" };

        var result = await manager.UpdateByFacilityIdAndLocationIdAsync("Fac-XYZ", "Loc-555", updateModel);

        Assert.Equal("New Name", result.LocationName);
    }

    [Fact]
    public async Task DeleteByIdAsync_Existing_DeletesRecord()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = "Delete-Test",
            LocationId = "Loc-999"
        });

        await manager.DeleteByIdAsync(created.LocationMappingId);

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        Assert.Null(await queries.GetByIdAsync(created.LocationMappingId));
    }

    [Fact]
    public async Task DeleteByFacilityIdAndLocationIdAsync_Existing_DeletesRecord()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);
        await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = "Fac-Delete",
            LocationId = "Loc-Delete"
        });

        await manager.DeleteByFacilityIdAndLocationIdAsync("Fac-Delete", "Loc-Delete");

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        Assert.Null(await queries.GetByFacilityIdAndLocationIdAsync("Fac-Delete", "Loc-Delete"));
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