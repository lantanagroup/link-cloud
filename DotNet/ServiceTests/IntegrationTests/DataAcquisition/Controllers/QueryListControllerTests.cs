using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Controllers;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class QueryListControllerTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public QueryListControllerTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private QueryListController CreateController(IServiceScope scope)
    {
        var logger = new Mock<ILogger<QueryListController>>().Object;
        var fhirQueryListConfigurationManager = scope.ServiceProvider.GetRequiredService<IFhirListQueryConfigurationManager>();
        var fhirQueryListConfigurationQueries = scope.ServiceProvider.GetRequiredService<IFhirQueryListConfigurationQueries>();
        return new QueryListController(logger, fhirQueryListConfigurationManager, fhirQueryListConfigurationQueries);
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
        var config = new FhirListConfiguration
        {
            FacilityId = "TestFacility",
            FhirBaseServerUrl = "http://example.com",
            EHRPatientLists = new List<EhrPatientList>() { new EhrPatientList() }
        };
        dbContext.FhirListConfigurations.Add(config);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.GetFhirConfiguration("TestFacility", CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsAssignableFrom<FhirListConfigurationModel>(okResult.Value);
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
        Assert.IsType<BadRequestResult>(result.Result);
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
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task PostFhirConfiguration_ValidModel_ReturnsOkWithConfiguration()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var controller = CreateController(scope);
        var config = new FhirListConfigurationModel
        {
            FacilityId = "TestFacility",
            FhirBaseServerUrl = "http://example.com",
            EHRPatientLists = new List<EhrPatientList>() { new EhrPatientList() }
        };

        // Act
        var result = await controller.PostFhirConfiguration(config, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsAssignableFrom<FhirListConfigurationModel>(okResult.Value);
    }

    [Fact]
    public async Task PostFhirConfiguration_InvalidModel_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var config = new FhirListConfigurationModel(); // Invalid, missing required fields

        // Act
        var result = await controller.PostFhirConfiguration(config, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task PostFhirConfiguration_Existing_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed existing
        var existing = new FhirListConfiguration
        {
            FacilityId = "TestFacility",
            FhirBaseServerUrl = "http://example.com",
            EHRPatientLists = new List<EhrPatientList>() { new EhrPatientList() }
        };
        dbContext.FhirListConfigurations.Add(existing);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var config = new FhirListConfigurationModel
        {
            FacilityId = "TestFacility",
            FhirBaseServerUrl = "http://example.com"
        };

        // Act
        var result = await controller.PostFhirConfiguration(config, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task PutFhirConfiguration_ValidModel_ReturnsOkWithConfiguration()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed existing
        var existing = new FhirListConfiguration
        {
            FacilityId = "TestFacility",
            FhirBaseServerUrl = "http://old.com",
            EHRPatientLists = new List<EhrPatientList>() { new EhrPatientList() }
        };
        dbContext.FhirListConfigurations.Add(existing);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var config = new FhirListConfigurationModel
        {
            FacilityId = "TestFacility",
            FhirBaseServerUrl = "http://new.com",
            EHRPatientLists = new List<EhrPatientList>() { new EhrPatientList() }
        };

        // Act
        var result = await controller.PutFhirConfiguration(config, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsAssignableFrom<FhirListConfigurationModel>(okResult.Value);
    }

    [Fact]
    public async Task PutFhirConfiguration_InvalidModel_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var config = new FhirListConfigurationModel(); // Invalid

        // Act
        var result = await controller.PutFhirConfiguration(config, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task PutFhirConfiguration_NonExisting_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var config = new FhirListConfigurationModel
        {
            FacilityId = "NonExisting",
            FhirBaseServerUrl = "http://example.com"
        };

        // Act
        var result = await controller.PutFhirConfiguration(config, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
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
        var config = new FhirListConfiguration
        {
            FacilityId = "TestFacility",
            FhirBaseServerUrl = "http://example.com",
            EHRPatientLists = new List<EhrPatientList>() {  new EhrPatientList() }
        };
        dbContext.FhirListConfigurations.Add(config);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.DeleteFhirConfiguration("TestFacility", CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);
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
        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task DeleteFhirConfiguration_NonExisting_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.DeleteFhirConfiguration("NonExisting", CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }
}