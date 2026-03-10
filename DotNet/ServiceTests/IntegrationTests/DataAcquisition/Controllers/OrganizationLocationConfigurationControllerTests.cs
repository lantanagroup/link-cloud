using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
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
public class OrganizationLocationConfigurationControllerTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public OrganizationLocationConfigurationControllerTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private OrganizationLocationConfigurationController CreateController(IServiceScope scope)
    {
        var logger = new Mock<ILogger<OrganizationLocationConfigurationController>>().Object;
        var manager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationManager>();
        var queries = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationQueries>();
        return new OrganizationLocationConfigurationController(logger, manager, queries);
    }

    #region GET /api/location-config/{id}

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsOk()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTime.UtcNow;
        var entity = new OrganizationLocationConfiguration
        {
            FacilityId = "TestFacility",
            Description = "Test Location Config",
            IsActive = true,
            CreatedOn = now,
            ModifiedOn = now
        };
        entity.LocationConditions.Add(new OrganizationLocationCondition
        {
            FhirPath = "Patient.location",
            Priority = 1,
            CreatedOn = now,
            ModifiedOn = now
        });

        dbContext.LocationConfigurations.Add(entity);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        var result = await controller.GetByIdAsync(entity.ConfigId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var model = Assert.IsAssignableFrom<OrganizationLocationConfigurationModel>(okResult.Value);
        Assert.Equal(entity.ConfigId, model.ConfigId);
        Assert.Equal("TestFacility", model.FacilityId);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNotFound()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        var result = await controller.GetByIdAsync(9999);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Not Found", problem.Title);
    }

    #endregion

    #region GET /api/location-config/facility/{facilityId}

    [Fact]
    public async Task GetByFacilityIdAsync_ExistingFacility_ReturnsOk()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTime.UtcNow;
        var entity = new OrganizationLocationConfiguration
        {
            FacilityId = "TestFacility",
            Description = "Test",
            CreatedOn = now,
            ModifiedOn = now
        };
        dbContext.LocationConfigurations.Add(entity);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        var result = await controller.GetByFacilityIdAsync("TestFacility");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<List<OrganizationLocationConfigurationModel>>(okResult.Value);
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
        var list = Assert.IsAssignableFrom<List<OrganizationLocationConfigurationModel>>(okResult.Value);
        Assert.Empty(list);
    }

    #endregion

    #region GET /api/location-config/facility/{facilityId}/search

    [Fact]
    public async Task SearchAsync_ValidFacility_ReturnsOkWithPagedResults()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTime.UtcNow;
        var entity = new OrganizationLocationConfiguration
        {
            FacilityId = "TestFacility",
            Description = "Test",
            CreatedOn = now,
            ModifiedOn = now
        };
        dbContext.LocationConfigurations.Add(entity);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var searchParams = new OrganizationLocationConfigurationSearchParameters();

        var result = await controller.SearchAsync("TestFacility", searchParams);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsAssignableFrom<PagedConfigModel<OrganizationLocationConfigurationModel>>(okResult.Value);
        Assert.Single(paged.Records);
    }

    [Fact]
    public async Task SearchAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        var result = await controller.SearchAsync(string.Empty, new OrganizationLocationConfigurationSearchParameters());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    #endregion

    #region POST /api/location-config/facility/{facilityId}

    [Fact]
    public async Task CreateAsync_ValidModel_ReturnsCreatedAtRoute()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        var apiModel = new CreateOrganizationLocationConfigurationApiModel
        {
            Description = "New Location Config",
            IsActive = true,
            Conditions = new List<CreateOrganizationLocationConditionModel>
            {
                new() { FhirPath = "Patient.location", Priority = 1 }
            }
        };

        var result = await controller.CreateAsync("TestFacility", apiModel);

        var createdResult = Assert.IsType<CreatedAtRouteResult>(result);
        Assert.Equal(nameof(OrganizationLocationConfigurationController.GetByIdAsync), createdResult.RouteName);
        Assert.IsAssignableFrom<OrganizationLocationConfigurationModel>(createdResult.Value);
    }

    [Fact]
    public async Task CreateAsync_NullModel_ReturnsBadRequest()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        var result = await controller.CreateAsync("TestFacility", null);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        var result = await controller.CreateAsync(string.Empty, new CreateOrganizationLocationConfigurationApiModel());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    #endregion

    #region PUT /api/location-config/{id}

    [Fact]
    public async Task UpdateByIdAsync_ExistingId_ReturnsAccepted()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTime.UtcNow;
        var entity = new OrganizationLocationConfiguration
        {
            FacilityId = "TestFacility",
            Description = "Old",
            CreatedOn = now,
            ModifiedOn = now
        };
        dbContext.LocationConfigurations.Add(entity);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var updateModel = new UpdateOrganizationLocationConfigurationModel { Description = "Updated" };

        var result = await controller.UpdateByIdAsync(entity.ConfigId, updateModel);

        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task UpdateByIdAsync_NonExistingId_ReturnsBadRequest()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        var result = await controller.UpdateByIdAsync(9999, new UpdateOrganizationLocationConfigurationModel());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
    }

    #endregion

    #region PUT /api/location-config/facility/{facilityId}

    [Fact]
    public async Task UpdateByFacilityIdAsync_ExistingFacility_ReturnsAccepted()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTime.UtcNow;
        var entity = new OrganizationLocationConfiguration
        {
            FacilityId = "TestFacility",
            CreatedOn = now,
            ModifiedOn = now
        };
        dbContext.LocationConfigurations.Add(entity);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var updateModel = new UpdateOrganizationLocationConfigurationModel { IsActive = false };

        var result = await controller.UpdateByFacilityIdAsync("TestFacility", updateModel);

        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task UpdateByFacilityIdAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        var result = await controller.UpdateByFacilityIdAsync(string.Empty, new UpdateOrganizationLocationConfigurationModel());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    #endregion

    #region DELETE /api/location-config/{id}

    [Fact]
    public async Task DeleteByIdAsync_ExistingId_ReturnsAccepted()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTime.UtcNow;
        var entity = new OrganizationLocationConfiguration
        {
            FacilityId = "TestFacility",
            CreatedOn = now,
            ModifiedOn = now
        };
        dbContext.LocationConfigurations.Add(entity);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        var result = await controller.DeleteByIdAsync(entity.ConfigId);

        Assert.IsType<AcceptedResult>(result);
    }

    #endregion

    #region DELETE /api/location-config/facility/{facilityId}

    [Fact]
    public async Task DeleteByFacilityIdAsync_ExistingFacility_ReturnsAccepted()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTime.UtcNow;
        var entity = new OrganizationLocationConfiguration
        {
            FacilityId = "TestFacility",
            CreatedOn = now,
            ModifiedOn = now
        };
        dbContext.LocationConfigurations.Add(entity);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        var result = await controller.DeleteByFacilityIdAsync("TestFacility");

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