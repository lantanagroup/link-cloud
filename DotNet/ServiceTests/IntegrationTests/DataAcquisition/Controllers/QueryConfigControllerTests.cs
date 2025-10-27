using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Models;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using System.Net;
using System.Net.Sockets;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Controllers;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class QueryConfigControllerTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public QueryConfigControllerTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private QueryConfigController CreateController(IServiceScope scope)
    {
        var logger = new Mock<ILogger<QueryConfigController>>().Object;

        var queryConfigurationManager = scope.ServiceProvider.GetRequiredService<IFhirQueryConfigurationManager>();
        var queryConfigurationQueries = scope.ServiceProvider.GetRequiredService<IFhirQueryConfigurationQueries>();

        var mocker = new AutoMocker();
        var tenantApiService = new Mock<ITenantApiService>();
            tenantApiService.Setup(x => x.GetFacilityConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LantanaGroup.Link.Shared.Application.Models.Tenant.FacilityModel { TimeZone = "America/Chicago" });

        return new QueryConfigController(logger, queryConfigurationManager, queryConfigurationQueries, tenantApiService.Object);
    }

    [Fact]
    public async Task GetFhirConfiguration_ValidFacilityId_ReturnsOkWithConfiguration()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed a configuration
        var config = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com"
            // Add other properties as needed
        };
        dbContext.FhirQueryConfigurations.Add(config);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.GetFhirConfiguration("TestFacility", CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsAssignableFrom<ApiResultFhirQueryConfigurationModel>(okResult.Value);
    }

    [Fact]
    public async Task GetFhirConfiguration_InvalidFacilityId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.GetFhirConfiguration(string.Empty, CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal((int)HttpStatusCode.BadRequest, problemResult.StatusCode);
    }

    [Fact]
    public async Task GetFhirConfiguration_NonExisting_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.GetFhirConfiguration("NonExisting", CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal((int)HttpStatusCode.NotFound, problemResult.StatusCode);
    }

    [Fact]
    public async Task CreateFhirConfiguration_ValidModel_ReturnsCreatedAtAction()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var controller = CreateController(scope);
        var model = new ApiCreateFhirQueryConfigurationModel
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com"
            // Add other properties as needed
        };

        // Act
        var result = await controller.CreateFhirConfiguration(model, CancellationToken.None);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal("CreateFhirConfiguration", createdResult.ActionName);
        Assert.IsAssignableFrom<ApiResultFhirQueryConfigurationModel>(createdResult.Value);
    }

    [Fact]
    public async Task CreateFhirConfiguration_NullModel_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.CreateFhirConfiguration(null, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task CreateFhirConfiguration_Existing_ReturnsConflict()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed existing
        var existing = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com"
        };
        dbContext.FhirQueryConfigurations.Add(existing);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var model = new ApiCreateFhirQueryConfigurationModel
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com"
        };

        // Act
        var result = await controller.CreateFhirConfiguration(model, CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal((int)HttpStatusCode.Conflict, problemResult.StatusCode);
    }

    [Fact]
    public async Task UpdateFhirConfiguration_ValidModel_ReturnsAccepted()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed existing
        var existing = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://old.com"
        };
        dbContext.FhirQueryConfigurations.Add(existing);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var model = new ApiUpdateFhirQueryConfigurationModel
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://new.com"
        };

        // Act
        var result = await controller.UpdateFhirConfiguration(model, CancellationToken.None);

        // Assert
        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task UpdateFhirConfiguration_NoChanges_ReturnsAccepted()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed existing
        var existing = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com"
        };
        dbContext.FhirQueryConfigurations.Add(existing);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var model = new ApiUpdateFhirQueryConfigurationModel
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com"
        };

        // Act
        var result = await controller.UpdateFhirConfiguration(model, CancellationToken.None);

        // Assert
        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task UpdateFhirConfiguration_NonExisting_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var model = new ApiUpdateFhirQueryConfigurationModel
        {
            FacilityId = "NonExisting",
            FhirServerBaseUrl = "http://example.com"
        };

        // Act
        var result = await controller.UpdateFhirConfiguration(model, CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, problemResult.StatusCode);
    }

    [Fact]
    public async Task UpdateFhirConfiguration_InvalidFacilityId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var model = new ApiUpdateFhirQueryConfigurationModel();

        // Act
        var result = await controller.UpdateFhirConfiguration(model, CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, problemResult.StatusCode);
    }

    [Fact]
    public async Task DeleteFhirConfiguration_ValidFacilityId_ReturnsAccepted()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed a configuration
        var config = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com"
        };
        dbContext.FhirQueryConfigurations.Add(config);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.DeleteFhirConfiguration("TestFacility", CancellationToken.None);

        // Assert
        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task DeleteFhirConfiguration_InvalidFacilityId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.DeleteFhirConfiguration(string.Empty, CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, problemResult.StatusCode);
    }

    [Fact]
    public async Task DeleteFhirConfiguration_NonExisting_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.DeleteFhirConfiguration("NonExisting", CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, problemResult.StatusCode);
    }
}