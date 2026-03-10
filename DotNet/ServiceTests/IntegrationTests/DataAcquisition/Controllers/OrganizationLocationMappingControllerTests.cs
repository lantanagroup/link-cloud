using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Controllers;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class OrganizationLocationMappingControllerTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public OrganizationLocationMappingControllerTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private OrganizationLocationMappingController CreateController(IServiceScope scope)
    {
        var logger = new Mock<ILogger<OrganizationLocationMappingController>>().Object;
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingQueries>();
        return new OrganizationLocationMappingController(logger, manager, queries);
    }

    private string GetUniqueFacilityId() => $"TestFac-{Guid.NewGuid():N}";
    private string GetUniqueLocationId() => $"Loc-{Guid.NewGuid():N}";

    #region GET /api/location-mappings/{id}

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsOk()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var facilityId = GetUniqueFacilityId();
        var locationId = GetUniqueLocationId();

        var created = await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = locationId,
            LocationName = "Test Location"
        });

        var controller = CreateController(scope);

        var result = await controller.GetByIdAsync(created.LocationMappingId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var model = Assert.IsAssignableFrom<OrganizationLocationMappingModel>(okResult.Value);
        Assert.Equal(created.LocationMappingId, model.LocationMappingId);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNotFound()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        var result = await controller.GetByIdAsync(9999);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
    }

    #endregion

    #region GET /api/location-mappings/facility/{facilityId}

    [Fact]
    public async Task GetByFacilityIdAsync_ExistingFacility_ReturnsOkWithList()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var facilityId = GetUniqueFacilityId();
        await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = GetUniqueLocationId()
        });

        var controller = CreateController(scope);

        var result = await controller.GetByFacilityIdAsync(facilityId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<List<OrganizationLocationMappingModel>>(okResult.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task GetByFacilityIdAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        var result = await controller.GetByFacilityIdAsync(string.Empty);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetByFacilityIdAsync_NonExistingFacility_ReturnsOkWithEmptyList()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        var result = await controller.GetByFacilityIdAsync("NonExisting");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<List<OrganizationLocationMappingModel>>(okResult.Value);
        Assert.Empty(list);
    }

    #endregion

    #region GET /api/location-mappings/facility/{facilityId}/search

    [Fact]
    public async Task SearchAsync_ValidFacility_ReturnsOkWithPagedResults()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var facilityId = GetUniqueFacilityId();
        await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = GetUniqueLocationId()
        });

        var controller = CreateController(scope);
        var searchParams = new OrganizationLocationMappingSearchParameters();

        var result = await controller.SearchAsync(facilityId, searchParams);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsAssignableFrom<PagedConfigModel<OrganizationLocationMappingModel>>(okResult.Value);
        Assert.Single(paged.Records);
    }

    [Fact]
    public async Task SearchAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        var result = await controller.SearchAsync(string.Empty, new OrganizationLocationMappingSearchParameters());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    #endregion

    #region PUT /api/location-mappings/{id}

    [Fact]
    public async Task UpdateByIdAsync_ExistingId_ReturnsAccepted()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var facilityId = GetUniqueFacilityId();
        var created = await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = GetUniqueLocationId()
        });

        var controller = CreateController(scope);
        var updateModel = new UpdateOrganizationLocationMappingModel { LocationName = "Updated Name" };

        var result = await controller.UpdateByIdAsync(created.LocationMappingId, updateModel);

        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task UpdateByIdAsync_NonExistingId_ReturnsNotFound()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        var result = await controller.UpdateByIdAsync(9999, new UpdateOrganizationLocationMappingModel());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
    }

    #endregion

    #region DELETE /api/location-mappings/facility/{facilityId}

    [Fact]
    public async Task DeleteByFacilityIdAsync_ExistingFacility_ReturnsAccepted()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationMappingManager>();
        var facilityId = GetUniqueFacilityId();

        await manager.CreateAsync(new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            LocationId = GetUniqueLocationId()
        });

        var controller = CreateController(scope);

        var result = await controller.DeleteByFacilityIdAsync(facilityId);

        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task DeleteByFacilityIdAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        var result = await controller.DeleteByFacilityIdAsync(string.Empty);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    #endregion
}