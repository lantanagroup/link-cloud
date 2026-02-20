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
public class OrganizationLocationConfigurationManagerTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public OrganizationLocationConfigurationManagerTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private IOrganizationLocationConfigurationManager CreateManager(IServiceScope scope)
    {
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        return new OrganizationLocationConfigurationManager(database);
    }

    [Fact]
    public async Task CreateAsync_ValidModelWithConditions_ReturnsModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);

        var createModel = new CreateOrganizationLocationConfigurationModel
        {
            FacilityId = "Nebraska-001",
            Description = "Nebraska Epic Config",
            IsActive = true,
            Conditions = new List<CreateOrganizationLocationConditionModel>
            {
                new() { FhirPath = "identifier.exists(system = 'urn:oid:1.2.840.114350.1.13.310.2.7.2.696570' and value = '10')", Priority = 1 }
            }
        };

        var result = await manager.CreateAsync(createModel);

        Assert.NotNull(result);
        Assert.Equal("Nebraska-001", result.FacilityId);
        Assert.Equal("Nebraska Epic Config", result.Description);
        Assert.True(result.IsActive);
        Assert.Single(result.Conditions);
        Assert.Equal("identifier.exists(system = 'urn:oid:1.2.840.114350.1.13.310.2.7.2.696570' and value = '10')", result.Conditions[0].FhirPath);
    }

    [Fact]
    public async Task UpdateByIdAsync_ValidUpdate_ReturnsUpdatedModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel
        {
            FacilityId = "Test-Update-Id",
            Description = "Old Description"
        });

        var updateModel = new UpdateOrganizationLocationConfigurationModel
        {
            Description = "New Description",
            IsActive = false,
            Conditions = new List<UpdateOrganizationLocationConditionModel>
            {
                new() { FhirPath = "managingOrganization.reference = 'Organization/123'", Priority = 1 }
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
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = "Multi-Facility", Description = "Config A" });
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = "Multi-Facility", Description = "Config B" });

        var updateModel = new UpdateOrganizationLocationConfigurationModel { Description = "All Updated" };

        var result = await manager.UpdateByFacilityIdAsync("Multi-Facility", updateModel);

        Assert.All(result, x => x.Description.Equals("All Updated"));
    }

    [Fact]
    public async Task DeleteByIdAsync_Existing_DeletesSuccessfully()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = "Delete-By-Id" });

        await manager.DeleteByIdAsync(created.ConfigId);

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>();
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
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = "Delete-All" });
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = "Delete-All" });

        await manager.DeleteByFacilityIdAsync("Delete-All");

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>();
        var search = await queries.SearchAsync(new OrganizationLocationConfigurationSearchModel { FacilityId = "Delete-All" });
        Assert.Empty(search.Records);
    }

    [Fact]
    public async Task UpdateByIdAsync_NotFound_ThrowsKeyNotFoundException()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            manager.UpdateByIdAsync(99999, new UpdateOrganizationLocationConfigurationModel()));
    }
}