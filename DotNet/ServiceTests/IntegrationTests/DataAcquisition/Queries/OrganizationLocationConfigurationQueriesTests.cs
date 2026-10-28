using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Queries;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class OrganizationLocationConfigurationQueriesTests
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public OrganizationLocationConfigurationQueriesTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static string NewFacilityId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    [Fact]
    public async Task GetByIdAsync_Exists_ReturnsModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationManager>();
        var facilityId = NewFacilityId("Get-By-Id-Test");
        var created = await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel
        {
            FacilityId = facilityId,
            Description = "Test GetById",
            Conditions = new List<CreateOrganizationLocationConditionModel> { new() { FhirPath = "identifier.value = '10'" } }
        });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>();
        var result = await queries.GetByIdAsync(created.ConfigId);

        Assert.NotNull(result);
        Assert.Equal(facilityId, result.FacilityId);
        Assert.Equal("Test GetById", result.Description);
        Assert.Single(result.Conditions);
    }

    [Fact]
    public async Task GetByFacilityIdAsync_Exists_ReturnsModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationManager>();
        var facilityId = NewFacilityId("Get-By-Facility");
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = facilityId, Description = "Facility Test" });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>();
        var result = await queries.GetByFacilityIdAsync(facilityId);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Equal(facilityId, result.First().FacilityId);
        Assert.Equal("Facility Test", result.First().Description);
    }

    [Fact]
    public async Task SearchAsync_NoFilters_ReturnsAllWithPagination()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationManager>();
        var facilityA = NewFacilityId("Facility-A");
        var facilityB = NewFacilityId("Facility-B");
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = facilityA });
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = facilityB });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>();
        var result = await queries.SearchAsync(new OrganizationLocationConfigurationSearchModel() { FacilityId = facilityA }, pageNumber: 1, pageSize: 10);

        Assert.Single(result.Records);
        Assert.Equal(1, result.Metadata.TotalCount);

        result = await queries.SearchAsync(new OrganizationLocationConfigurationSearchModel() { FacilityId = facilityB }, pageNumber: 1, pageSize: 10);

        Assert.Single(result.Records);
        Assert.Equal(1, result.Metadata.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_WithFilters_ReturnsFilteredResults()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationManager>();
        var facility300 = NewFacilityId("Facility-300");
        var facility400 = NewFacilityId("Facility-400");
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = facility300, Description = "Nebraska", IsActive = true });
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = facility400, Description = "Michigan", IsActive = false });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>();

        var result = await queries.SearchAsync(new OrganizationLocationConfigurationSearchModel
        {
            FacilityId = facility300,
            IsActive = true,
            DescriptionContains = "Nebraska"
        });

        Assert.Single(result.Records);
        Assert.Equal(facility300, result.Records[0].FacilityId);
    }

    [Fact]
    public async Task SearchAsync_Pagination_WorksCorrectly()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationManager>();
        var facilityId = NewFacilityId("Facility-Test");
        for (int i = 1; i <= 25; i++)
            await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = facilityId });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>();
        var result = await queries.SearchAsync(new OrganizationLocationConfigurationSearchModel() { FacilityId = facilityId }, pageNumber: 2, pageSize: 10);

        Assert.Equal(10, result.Records.Count);
        Assert.Equal(25, result.Metadata.TotalCount);
        Assert.Equal(2, result.Metadata.PageNumber);
    }
}
