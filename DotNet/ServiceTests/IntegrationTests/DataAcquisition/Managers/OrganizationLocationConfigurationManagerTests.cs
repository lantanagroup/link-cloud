using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Managers;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class OrganizationLocationConfigurationManagerTests
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public OrganizationLocationConfigurationManagerTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private IOrganizationLocationConfigurationManager CreateManager(IServiceScope scope)
    {
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>();
        var locationResolutionValidator = scope.ServiceProvider.GetRequiredService<ILocationResolutionValidator>();
        return new OrganizationLocationConfigurationManager(database, queries, locationResolutionValidator);
    }

    private static string NewFacilityId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    [Fact]
    public async Task CreateAsync_ValidModelWithConditions_ReturnsModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var facilityId = NewFacilityId("Nebraska");

        var createModel = new CreateOrganizationLocationConfigurationModel
        {
            FacilityId = facilityId,
            Description = "Nebraska Epic Config",
            IsActive = true,
            Conditions = new List<CreateOrganizationLocationConditionModel>
            {
                new() { FhirPath = "identifier.exists(system = 'urn:oid:1.2.840.114350.1.13.310.2.7.2.696570' and value = '10')", Priority = 1 }
            }
        };

        var result = await manager.CreateAsync(createModel);

        Assert.NotNull(result);
        Assert.Equal(facilityId, result.FacilityId);
        Assert.Equal("Nebraska Epic Config", result.Description);
        Assert.True(result.IsActive);
        Assert.Single(result.Conditions);
        Assert.Equal("identifier.exists(system = 'urn:oid:1.2.840.114350.1.13.310.2.7.2.696570' and value = '10')", result.Conditions[0].FhirPath);
    }

    [Fact]
    public async Task UpdateByIdAsync_ValidUpdate_ReturnsUpdatedModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel
        {
            FacilityId = NewFacilityId("Test-Update-Id"),
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
        var manager = CreateManager(scope);
        var facilityId = NewFacilityId("Multi-Facility");
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = facilityId, Description = "Config A" });
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = facilityId, Description = "Config B" });

        var updateModel = new UpdateOrganizationLocationConfigurationModel { Description = "All Updated" };

        var result = await manager.UpdateByFacilityIdAsync(facilityId, updateModel);

        Assert.All(result, x => x.Description.Equals("All Updated"));
    }

    [Fact]
    public async Task DeleteByIdAsync_Existing_DeletesSuccessfully()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var created = await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = NewFacilityId("Delete-By-Id") });

        await manager.DeleteByIdAsync(created.ConfigId);

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => queries.GetByIdAsync(created.ConfigId));
    }

    [Fact]
    public async Task DeleteByFacilityIdAsync_Multiple_DeletesAll()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var facilityId = NewFacilityId("Delete-All");
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = facilityId });
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = facilityId });

        await manager.DeleteByFacilityIdAsync(facilityId);

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>();
        var search = await queries.SearchAsync(new OrganizationLocationConfigurationSearchModel { FacilityId = facilityId });
        Assert.Empty(search.Records);
    }

    [Fact]
    public async Task UpdateByIdAsync_NotFound_ThrowsKeyNotFoundException()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            manager.UpdateByIdAsync(99999, new UpdateOrganizationLocationConfigurationModel()));
    }
}
