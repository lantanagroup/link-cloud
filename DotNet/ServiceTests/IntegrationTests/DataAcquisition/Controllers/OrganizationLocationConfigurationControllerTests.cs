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

    #region GET /location-org-configs/{id}

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsOk()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var entity = new OrganizationLocationConfiguration
        {
            FacilityId = "TestFacility",
            Description = "Test Location Config",
            IsActive = true
        };
        entity.LocationConditions.Add(new OrganizationLocationCondition { FhirPath = "Patient.location", Priority = 1 });
        dbContext.LocationConfigurations.Add(entity);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.GetByIdAsync(entity.ConfigId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var model = Assert.IsAssignableFrom<OrganizationLocationConfigurationModel>(okResult.Value);
        Assert.Equal(entity.ConfigId, model.ConfigId);
        Assert.Equal("TestFacility", model.FacilityId);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.GetByIdAsync(9999);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Not Found", problem.Title);
    }

    #endregion

    #region GET /location-org-configs/facilities/{facilityId}

    [Fact]
    public async Task GetByFacilityIdAsync_ExistingFacility_ReturnsOk()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var entity = new OrganizationLocationConfiguration { FacilityId = "TestFacility", Description = "Test" };
        dbContext.LocationConfigurations.Add(entity);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.GetByFacilityIdAsync("TestFacility");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsAssignableFrom<OrganizationLocationConfigurationModel>(okResult.Value);
    }

    [Fact]
    public async Task GetByFacilityIdAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.GetByFacilityIdAsync(string.Empty);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Bad Request", problem.Title);
    }

    [Fact]
    public async Task GetByFacilityIdAsync_NonExistingFacility_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.GetByFacilityIdAsync("NonExisting");

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
    }

    #endregion

    #region GET /location-org-configs/facilities/{facilityId}/search

    [Fact]
    public async Task SearchAsync_ValidFacility_ReturnsOkWithPagedResults()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var entity = new OrganizationLocationConfiguration { FacilityId = "TestFacility", Description = "Test" };
        dbContext.LocationConfigurations.Add(entity);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var searchParams = new OrganizationLocationConfigurationSearchParameters();

        // Act
        var result = await controller.SearchAsync("TestFacility", searchParams);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsAssignableFrom<PagedConfigModel<OrganizationLocationConfigurationModel>>(okResult.Value);
        Assert.Single(paged.Records);
    }

    [Fact]
    public async Task SearchAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.SearchAsync(string.Empty, new OrganizationLocationConfigurationSearchParameters());

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    #endregion

    #region POST /location-org-configs/facilities/{facilityId}

    [Fact]
    public async Task CreateAsync_ValidModel_ReturnsCreatedAtAction()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

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

        // Act
        var result = await controller.CreateAsync("TestFacility", apiModel);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(OrganizationLocationConfigurationController.GetByIdAsync), createdResult.ActionName);
        Assert.IsAssignableFrom<OrganizationLocationConfigurationModel>(createdResult.Value);
    }

    [Fact]
    public async Task CreateAsync_NullModel_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.CreateAsync("TestFacility", null);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.CreateAsync(string.Empty, new CreateOrganizationLocationConfigurationApiModel());

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    #endregion

    #region PUT /location-org-configs/{id}

    [Fact]
    public async Task UpdateByIdAsync_ExistingId_ReturnsAccepted()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var entity = new OrganizationLocationConfiguration { FacilityId = "TestFacility", Description = "Old" };
        dbContext.LocationConfigurations.Add(entity);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var updateModel = new UpdateOrganizationLocationConfigurationModel { Description = "Updated" };

        // Act
        var result = await controller.UpdateByIdAsync(entity.ConfigId, updateModel);

        // Assert
        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task UpdateByIdAsync_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.UpdateByIdAsync(9999, new UpdateOrganizationLocationConfigurationModel());

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
    }

    #endregion

    #region PUT /location-org-configs/facilities/{facilityId}

    [Fact]
    public async Task UpdateByFacilityIdAsync_ExistingFacility_ReturnsAccepted()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var entity = new OrganizationLocationConfiguration { FacilityId = "TestFacility" };
        dbContext.LocationConfigurations.Add(entity);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var updateModel = new UpdateOrganizationLocationConfigurationModel { IsActive = false };

        // Act
        var result = await controller.UpdateByFacilityIdAsync("TestFacility", updateModel);

        // Assert
        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task UpdateByFacilityIdAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.UpdateByFacilityIdAsync(string.Empty, new UpdateOrganizationLocationConfigurationModel());

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    #endregion

    #region DELETE /location-org-configs/{id}

    [Fact]
    public async Task DeleteByIdAsync_ExistingId_ReturnsAccepted()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var entity = new OrganizationLocationConfiguration { FacilityId = "TestFacility" };
        dbContext.LocationConfigurations.Add(entity);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.DeleteByIdAsync(entity.ConfigId);

        // Assert
        Assert.IsType<AcceptedResult>(result);
    }

    #endregion

    #region DELETE /location-org-configs/facilities/{facilityId}

    [Fact]
    public async Task DeleteByFacilityIdAsync_ExistingFacility_ReturnsAccepted()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var entity = new OrganizationLocationConfiguration { FacilityId = "TestFacility" };
        dbContext.LocationConfigurations.Add(entity);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.DeleteByFacilityIdAsync("TestFacility");

        // Assert
        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task DeleteByFacilityIdAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.DeleteByFacilityIdAsync(string.Empty);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    #endregion
}