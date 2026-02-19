using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Queries;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class LocationConfigurationQueriesTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public LocationConfigurationQueriesTests(DataAcquisitionIntegrationTestFixture fixture)
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

        var manager = scope.ServiceProvider.GetRequiredService<ILocationConfigurationManager>();
        var created = await manager.CreateAsync(new CreateLocationConfigurationModel
        {
            FacilityId = 555,
            Description = "Test GetById",
            Conditions = new List<CreateLocationConditionModel> { new() { FhirPath = "identifier.value = '10'" } }
        });

        var queries = scope.ServiceProvider.GetRequiredService<ILocationConfigurationQueries>();
        var result = await queries.GetByIdAsync(created.ConfigId);

        Assert.NotNull(result);
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

        var manager = scope.ServiceProvider.GetRequiredService<ILocationConfigurationManager>();
        await manager.CreateAsync(new CreateLocationConfigurationModel { FacilityId = 666, Description = "Facility Test" });

        var queries = scope.ServiceProvider.GetRequiredService<ILocationConfigurationQueries>();
        var result = await queries.GetByFacilityIdAsync(666);

        Assert.NotNull(result);
        Assert.Equal("Facility Test", result.Description);
    }

    [Fact]
    public async Task SearchAsync_NoFilters_ReturnsAllWithPagination()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<ILocationConfigurationManager>();
        await manager.CreateAsync(new CreateLocationConfigurationModel { FacilityId = 100 });
        await manager.CreateAsync(new CreateLocationConfigurationModel { FacilityId = 200 });

        var queries = scope.ServiceProvider.GetRequiredService<ILocationConfigurationQueries>();
        var result = await queries.SearchAsync(new LocationConfigurationSearchModel(), pageNumber: 1, pageSize: 10);

        Assert.Equal(2, result.Records.Count);
        Assert.Equal(2, result.Metadata.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_WithFilters_ReturnsFilteredResults()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<ILocationConfigurationManager>();
        await manager.CreateAsync(new CreateLocationConfigurationModel { FacilityId = 300, Description = "Nebraska", IsActive = true });
        await manager.CreateAsync(new CreateLocationConfigurationModel { FacilityId = 400, Description = "Michigan", IsActive = false });

        var queries = scope.ServiceProvider.GetRequiredService<ILocationConfigurationQueries>();

        var result = await queries.SearchAsync(new LocationConfigurationSearchModel
        {
            FacilityId = 300,
            IsActive = true,
            DescriptionContains = "Nebraska"
        });

        Assert.Single(result.Records);
        Assert.Equal(300, result.Records[0].FacilityId);
    }

    [Fact]
    public async Task SearchAsync_Pagination_WorksCorrectly()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<ILocationConfigurationManager>();
        for (int i = 1; i <= 25; i++)
            await manager.CreateAsync(new CreateLocationConfigurationModel { FacilityId = i });

        var queries = scope.ServiceProvider.GetRequiredService<ILocationConfigurationQueries>();
        var result = await queries.SearchAsync(new LocationConfigurationSearchModel(), pageNumber: 2, pageSize: 10);

        Assert.Equal(10, result.Records.Count);
        Assert.Equal(25, result.Metadata.TotalCount);
        Assert.Equal(2, result.Metadata.PageNumber);
    }
}