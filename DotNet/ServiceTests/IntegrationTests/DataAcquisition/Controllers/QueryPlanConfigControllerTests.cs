using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Controllers;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class QueryPlanConfigControllerTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public QueryPlanConfigControllerTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private QueryPlanConfigController CreateController(IServiceScope scope)
    {
        var logger = new Mock<ILogger<QueryPlanConfigController>>().Object;
        var queryPlanManager = scope.ServiceProvider.GetRequiredService<IQueryPlanManager>();
        var queryPlanQueries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
        return new QueryPlanConfigController(logger, queryPlanManager, queryPlanQueries);
    }

    [Fact]
    public async Task GetQueryPlan_ValidParameters_ReturnsOkWithQueryPlan()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed a query plan
        var queryPlan = new QueryPlan
        {
            FacilityId = "TestFacility",
            Type = Frequency.Daily,
            PlanName = "TestPlan",
            EHRDescription = "Test EHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
        dbContext.QueryPlan.Add(queryPlan);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var queryParams = new GetQueryPlanParameters
        {
            Type = Frequency.Daily
        };

        // Act
        var result = await controller.GetQueryPlan("TestFacility", queryParams, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsAssignableFrom<QueryPlanModel>(okResult.Value);
    }

    [Fact]
    public async Task GetQueryPlan_MissingType_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.GetQueryPlan("TestFacility", new GetQueryPlanParameters(), CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, problemResult.StatusCode);
    }

    [Fact]
    public async Task GetQueryPlan_InvalidFacilityId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var queryParams = new GetQueryPlanParameters
        {
            Type = Frequency.Daily
        };

        // Act
        var result = await controller.GetQueryPlan(string.Empty, queryParams, CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, problemResult.StatusCode);
    }

    [Fact]
    public async Task GetQueryPlan_NonExisting_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var queryParams = new GetQueryPlanParameters
        {
            Type = Frequency.Daily
        };

        // Act
        var result = await controller.GetQueryPlan("NonExistingFacility", queryParams, CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, problemResult.StatusCode);
    }

    [Fact]
    public async Task CreateQueryPlan_ValidModel_ReturnsCreatedAtAction()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var controller = CreateController(scope);
        var postModel = new QueryPlanPostModel
        {
            PlanName = "TestPlan",
            Type = Frequency.Daily,
            FacilityId = "TestFacility",
            EHRDescription = "Test EHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>() { { "QueryPlan", new IQueryConfig() } },
            SupplementalQueries = new Dictionary<string, IQueryConfig>() { { "QueryPlan", new IQueryConfig() } }
        };

        // Act
        var result = await controller.CreateQueryPlan("TestFacility", postModel, CancellationToken.None);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal("CreateQueryPlan", createdResult.ActionName);
        Assert.IsAssignableFrom<QueryPlanModel>(createdResult.Value);
    }

    [Fact]
    public async Task CreateQueryPlan_NullBody_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.CreateQueryPlan("TestFacility", null, CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, problemResult.StatusCode);
    }

    [Fact]
    public async Task CreateQueryPlan_InvalidFacilityId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var postModel = new QueryPlanPostModel
        {
            FacilityId = null,
            Type = Frequency.Daily
        };

        // Act
        var result = await controller.CreateQueryPlan(string.Empty, postModel, CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, problemResult.StatusCode);
    }

    [Fact]
    public async Task CreateQueryPlan_Existing_ReturnsConflict()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = "TestFacility",
            Type = Frequency.Daily,
            PlanName = "TestPlan",
            EHRDescription = "Test EHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
        dbContext.QueryPlan.Add(existing);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var postModel = new QueryPlanPostModel
        {
            PlanName = "TestPlan",
            Type = Frequency.Daily,
            FacilityId = "TestFacility",
            EHRDescription = "Test EHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };

        // Act
        var result = await controller.CreateQueryPlan("TestFacility", postModel, CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.Conflict, problemResult.StatusCode);
    }

    [Fact]
    public async Task UpdateQueryPlan_ValidModel_ReturnsAccepted()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed existing
        var existing = new QueryPlan
        {
            FacilityId = "TestFacility",
            Type = Frequency.Daily,
            PlanName = "TestPlan",
            EHRDescription = "Test EHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>() { { "QueryPlan", new IQueryConfig() } },
            SupplementalQueries = new Dictionary<string, IQueryConfig>() { { "QueryPlan", new IQueryConfig() } }
        };
        dbContext.QueryPlan.Add(existing);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var putModel = new QueryPlanPutModel
        {
            Id = existing.Id,
            PlanName = "UpdatedPlan",
            Type = Frequency.Daily,
            FacilityId = "TestFacility",
            EHRDescription = "Updated EHR",
            LookBack = "2d",
            InitialQueries = new Dictionary<string, IQueryConfig>() { { "QueryPlan", new IQueryConfig() } },
            SupplementalQueries = new Dictionary<string, IQueryConfig>() { { "QueryPlan", new IQueryConfig() } }
        };

        // Act
        var result = await controller.UpdateQueryPlan("TestFacility", putModel, CancellationToken.None);

        // Assert
        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task UpdateQueryPlan_NonExisting_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var putModel = new QueryPlanPutModel
        {
            Id = Guid.NewGuid(),
            FacilityId = "testFacility",
            Type = Frequency.Daily
        };

        // Act
        var result = await controller.UpdateQueryPlan("NonExistingFacility", putModel, CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, problemResult.StatusCode);
    }

    [Fact]
    public async Task UpdateQueryPlan_InvalidFacilityId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var putModel = new QueryPlanPutModel
        {
            Id = Guid.NewGuid(),
            FacilityId = null,
            Type = Frequency.Daily
        };

        // Act
        var result = await controller.UpdateQueryPlan(string.Empty, putModel, CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, problemResult.StatusCode);
    }

    [Fact]
    public async Task DeleteQueryPlan_ValidParameters_ReturnsAccepted()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed a query plan
        var queryPlan = new QueryPlan
        {
            FacilityId = "TestFacility",
            Type = Frequency.Monthly,
            PlanName = "TestPlan",
            EHRDescription = "Test EHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>() { { "QueryPlan", new IQueryConfig() } },
            SupplementalQueries = new Dictionary<string, IQueryConfig>() { { "QueryPlan", new IQueryConfig() } }
        };
        dbContext.QueryPlan.Add(queryPlan);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var deleteParams = new DeleteQueryPlanParameters
        {
            Type = Frequency.Monthly
        };

        // Act
        var result = await controller.DeleteQueryPlan("TestFacility", deleteParams, CancellationToken.None);

        // Assert
        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task DeleteQueryPlan_MissingType_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.DeleteQueryPlan("TestFacility", new DeleteQueryPlanParameters(), CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, problemResult.StatusCode);
    }

    [Fact]
    public async Task DeleteQueryPlan_InvalidFacilityId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var deleteParams = new DeleteQueryPlanParameters
        {
            Type = Frequency.Monthly
        };

        // Act
        var result = await controller.DeleteQueryPlan(string.Empty, deleteParams, CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, problemResult.StatusCode);
    }

    [Fact]
    public async Task DeleteQueryPlan_NonExisting_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var deleteParams = new DeleteQueryPlanParameters
        {
            Type = Frequency.Monthly
        };

        // Act
        var result = await controller.DeleteQueryPlan("NonExistingFacility", deleteParams, CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, problemResult.StatusCode);
    }
}