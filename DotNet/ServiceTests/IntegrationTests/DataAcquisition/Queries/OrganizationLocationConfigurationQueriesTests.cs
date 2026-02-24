using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Queries;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class OrganizationLocationConfigurationQueriesTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public OrganizationLocationConfigurationQueriesTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetByIdAsync_Exists_ReturnsModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationManager>();
        var created = await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel
        {
            FacilityId = "Get-By-Id-Test",
            Description = "Test GetById",
            Conditions = new List<CreateOrganizationLocationConditionModel> { new() { FhirPath = "identifier.value = '10'" } }
        });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>();
        var result = await queries.GetByIdAsync(created.ConfigId);

        Assert.NotNull(result);
        Assert.Equal("Get-By-Id-Test", result.FacilityId);
        Assert.Equal("Test GetById", result.Description);
        Assert.Single(result.Conditions);
    }

    [Fact]
    public async Task GetByFacilityIdAsync_Exists_ReturnsModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationManager>();
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = "Get-By-Facility", Description = "Facility Test" });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>();
        var result = await queries.GetByFacilityIdAsync("Get-By-Facility");

        Assert.NotNull(result);
        Assert.Equal("Get-By-Facility", result.FacilityId);
        Assert.Equal("Facility Test", result.Description);
    }

    [Fact]
    public async Task SearchAsync_NoFilters_ReturnsAllWithPagination()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationManager>();
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = "Facility-A" });
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = "Facility-B" });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>();
        var result = await queries.SearchAsync(new OrganizationLocationConfigurationSearchModel() { FacilityId = "Facility-A" }, pageNumber: 1, pageSize: 10);

        Assert.Single(result.Records);
        Assert.Equal(1, result.Metadata.TotalCount);

        result = await queries.SearchAsync(new OrganizationLocationConfigurationSearchModel() { FacilityId = "Facility-B" }, pageNumber: 1, pageSize: 10);

        Assert.Single(result.Records);
        Assert.Equal(1, result.Metadata.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_WithFilters_ReturnsFilteredResults()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationManager>();
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = "Facility-300", Description = "Nebraska", IsActive = true });
        await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = "Facility-400", Description = "Michigan", IsActive = false });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>();

        var result = await queries.SearchAsync(new OrganizationLocationConfigurationSearchModel
        {
            FacilityId = "Facility-300",
            IsActive = true,
            DescriptionContains = "Nebraska"
        });

        Assert.Single(result.Records);
        Assert.Equal("Facility-300", result.Records[0].FacilityId);
    }

    [Fact]
    public async Task SearchAsync_Pagination_WorksCorrectly()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationManager>();
        for (int i = 1; i <= 25; i++)
            await manager.CreateAsync(new CreateOrganizationLocationConfigurationModel { FacilityId = $"Facility-Test" });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>();
        var result = await queries.SearchAsync(new OrganizationLocationConfigurationSearchModel() { FacilityId = "Facility-Test" }, pageNumber: 2, pageSize: 10);

        Assert.Equal(10, result.Records.Count);
        Assert.Equal(25, result.Metadata.TotalCount);
        Assert.Equal(2, result.Metadata.PageNumber);
    }
}