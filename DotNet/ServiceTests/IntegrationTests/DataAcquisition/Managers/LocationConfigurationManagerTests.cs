using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Managers;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class LocationConfigurationManagerTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public LocationConfigurationManagerTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private ILocationConfigurationManager CreateManager(IServiceScope scope)
    {
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        return new LocationConfigurationManager(database);
    }

    [Fact]
    public async Task CreateAsync_ValidModelWithConditions_ReturnsModel()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);

        var createModel = new CreateLocationConfigurationModel
        {
            FacilityId = 123,
            Description = "Nebraska Epic Config",
            IsActive = true,
            Conditions = new List<CreateLocationConditionModel>
            {
                new() { FhirPath = "identifier.exists(system = 'urn:oid:1.2.840.114350.1.13.310.2.7.2.696570' and value = '10')", Priority = 1 }
            }
        };

        // Act
        var result = await manager.CreateAsync(createModel);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(123, result.FacilityId);
        Assert.Equal("Nebraska Epic Config", result.Description);
        Assert.True(result.IsActive);
        Assert.Single(result.Conditions);
        Assert.Equal("identifier.exists(system = 'urn:oid:1.2.840.114350.1.13.310.2.7.2.696570' and value = '10')", result.Conditions[0].FhirPath);
    }

    [Fact]
    public async Task CreateAsync_ValidModelNoConditions_ReturnsModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);

        var createModel = new CreateLocationConfigurationModel
        {
            FacilityId = 456,
            Description = "Simple Config",
            IsActive = false
        };

        var result = await manager.CreateAsync(createModel);

        Assert.NotNull(result);
        Assert.Equal(456, result.FacilityId);
        Assert.Empty(result.Conditions);
    }

    [Fact]
    public async Task UpdateByIdAsync_ValidUpdate_ReturnsUpdatedModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);

        var created = await manager.CreateAsync(new CreateLocationConfigurationModel { FacilityId = 789, Description = "Old" });

        var updateModel = new UpdateLocationConfigurationModel
        {
            Description = "New Description",
            IsActive = false,
            Conditions = new List<UpdateLocationConditionModel>
            {
                new() { FhirPath = "managingOrganization.reference = 'Org/123'", Priority = 1 }
            }
        };

        var result = await manager.UpdateByIdAsync(created.ConfigId, updateModel);

        Assert.Equal("New Description", result.Description);
        Assert.False(result.IsActive);
        Assert.Single(result.Conditions);
    }

    [Fact]
    public async Task UpdateByFacilityIdAsync_MultipleConfigs_UpdatesAll()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);

        await manager.CreateAsync(new CreateLocationConfigurationModel { FacilityId = 999, Description = "Config1" });
        await manager.CreateAsync(new CreateLocationConfigurationModel { FacilityId = 999, Description = "Config2" });

        var updateModel = new UpdateLocationConfigurationModel { Description = "Updated All" };

        var result = await manager.UpdateByFacilityIdAsync(999, updateModel);

        Assert.Equal("Updated All", result.Description); // returns first one
    }

    [Fact]
    public async Task DeleteByIdAsync_Existing_Deletes()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateLocationConfigurationModel { FacilityId = 111 });

        await manager.DeleteByIdAsync(created.ConfigId);

        var queries = scope.ServiceProvider.GetRequiredService<ILocationConfigurationQueries>();
        Assert.Null(await queries.GetByIdAsync(created.ConfigId));
    }

    [Fact]
    public async Task DeleteByFacilityIdAsync_Multiple_DeletesAll()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);
        await manager.CreateAsync(new CreateLocationConfigurationModel { FacilityId = 222 });
        await manager.CreateAsync(new CreateLocationConfigurationModel { FacilityId = 222 });

        await manager.DeleteByFacilityIdAsync(222);

        var queries = scope.ServiceProvider.GetRequiredService<ILocationConfigurationQueries>();
        var search = await queries.SearchAsync(new LocationConfigurationSearchModel { FacilityId = 222 });
        Assert.Empty(search.Records);
    }

    [Fact]
    public async Task UpdateByIdAsync_NotFound_Throws()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            manager.UpdateByIdAsync(9999, new UpdateLocationConfigurationModel()));
    }
}