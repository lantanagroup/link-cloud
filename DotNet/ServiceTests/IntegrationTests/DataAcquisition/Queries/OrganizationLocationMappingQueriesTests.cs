using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Queries;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Queries;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class OrganizationLocationMappingQueriesTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public OrganizationLocationMappingQueriesTests(DataAcquisitionIntegrationTestFixture fixture)
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

        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var created = await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = "Test-Fac",
            LocationId = "Loc-001",
            LocationName = "Test Bed",
            IsOrgLocation = true
        });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var result = await queries.GetByIdAsync(created.LocationMappingId);

        Assert.NotNull(result);
        Assert.Equal("Test-Fac", result.FacilityId);
        Assert.Equal("Loc-001", result.LocationId);
        Assert.Equal("Test Bed", result.LocationName);
    }

    [Fact]
    public async Task GetByFacilityIdAndLocationIdAsync_Exists_ReturnsModel()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = "Fac-123",
            LocationId = "Loc-XYZ"
        });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var result = await queries.GetByFacilityIdAndLocationIdAsync("Fac-123", "Loc-XYZ");

        Assert.NotNull(result);
        Assert.Equal("Fac-123", result.FacilityId);
    }

    [Fact]
    public async Task GetByFacilityIdAsync_ReturnsAllForFacility()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        await manager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = "Fac-ABC", LocationId = "Loc-1" });
        await manager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = "Fac-ABC", LocationId = "Loc-2" });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var results = await queries.GetByFacilityIdAsync("Fac-ABC");

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task SearchAsync_WithFilters_ReturnsCorrectResults()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        await manager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = "Fac-Filter", LocationId = "Loc-A", IsOrgLocation = true });
        await manager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = "Fac-Filter", LocationId = "Loc-B", IsOrgLocation = false });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();

        var result = await queries.SearchAsync(new OrganizationLocationMappingSearchModel
        {
            FacilityId = "Fac-Filter",
            IsOrgLocation = true
        });

        Assert.Single(result.Records);
        Assert.True(result.Records[0].IsOrgLocation);
    }

    [Fact]
    public async Task SearchAsync_Pagination_WorksCorrectly()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        for (int i = 1; i <= 15; i++)
            await manager.CreateAsync(new CreateOrganizationLocationMappingModel { FacilityId = "Pag-Test", LocationId = $"Loc-{i}" });

        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        var result = await queries.SearchAsync(new OrganizationLocationMappingSearchModel { FacilityId = "Pag-Test" }, pageNumber: 2, pageSize: 5);

        Assert.Equal(5, result.Records.Count);
        Assert.Equal(15, result.Metadata.TotalCount);
    }
}