using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    private QueryPlanConfigController CreateControllerWithTenantMock(IServiceScope scope, Mock<ITenantApiService> tenantApiServiceMock)
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
        dbContext.QueryPlans.Add(queryPlan);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var queryParams = new GetQueryPlanParameters { Type = Frequency.Daily };

        // Act
        var result = await controller.GetQueryPlan("TestFacility", queryParams, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedModel = Assert.IsType<QueryPlanModel>(okResult.Value);
        Assert.Equal("TestFacility", returnedModel.FacilityId);
        Assert.Equal(Frequency.Daily, returnedModel.Type);
        Assert.Equal("TestPlan", returnedModel.PlanName);
        Assert.Equal("Test EHR", returnedModel.EHRDescription);
        Assert.Equal("1d", returnedModel.LookBack);
        Assert.Empty(returnedModel.InitialQueries);
        Assert.Empty(returnedModel.SupplementalQueries);
    }

    [Fact]
    public async Task GetQueryPlan_MissingFacilityId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var queryParams = new GetQueryPlanParameters { Type = Frequency.Daily };

        // Act
        var result = await controller.GetQueryPlan(string.Empty, queryParams, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        Assert.Equal("Bad Request", ((ProblemDetails)objectResult.Value).Title);
        Assert.Contains("facilityId is required", ((ProblemDetails)objectResult.Value).Detail, StringComparison.OrdinalIgnoreCase);
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
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        Assert.Equal("Bad Request", ((ProblemDetails)objectResult.Value).Title);
        Assert.Contains("type query parameter must be defined", ((ProblemDetails)objectResult.Value).Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetQueryPlan_InvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        controller.ModelState.AddModelError("Type", "Invalid");
        var queryParams = new GetQueryPlanParameters { Type = Frequency.Daily };

        // Act
        var result = await controller.GetQueryPlan("TestFacility", queryParams, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task GetQueryPlan_NotFound_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var controller = CreateController(scope);
        var queryParams = new GetQueryPlanParameters { Type = Frequency.Daily };

        // Act
        var result = await controller.GetQueryPlan("NonExisting", queryParams, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
        Assert.Equal("Not Found", ((ProblemDetails)objectResult.Value).Title);
        Assert.Contains("No Query Plan found", ((ProblemDetails)objectResult.Value).Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetQueryPlan_Exception_ReturnsInternalServerError()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var queryPlanQueriesMock = new Mock<IQueryPlanQueries>();
        queryPlanQueriesMock.Setup(q => q.GetAsync(It.IsAny<string>(), It.IsAny<Frequency>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        var logger = new Mock<ILogger<QueryPlanConfigController>>().Object;
        var queryPlanManager = scope.ServiceProvider.GetRequiredService<IQueryPlanManager>();
        var controller = new QueryPlanConfigController(logger, queryPlanManager, queryPlanQueriesMock.Object);

        var queryParams = new GetQueryPlanParameters { Type = Frequency.Daily };

        // Act
        var result = await controller.GetQueryPlan("TestFacility", queryParams, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
        Assert.Equal("Internal Server Error", ((ProblemDetails)objectResult.Value).Title);
    }

    [Fact]
    public async Task CreateQueryPlan_ValidModel_ReturnsCreated()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var controller = CreateController(scope);
        var model = CreateValidQueryPlanApiModel();

        // Act
        var result = await controller.CreateQueryPlan("TestFacility", model, CancellationToken.None);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var returnedModel = Assert.IsType<QueryPlanModel>(createdResult.Value);
        Assert.Equal("TestFacility", returnedModel.FacilityId);
        Assert.Equal(Frequency.Daily, returnedModel.Type);
        Assert.Equal("TestPlan", returnedModel.PlanName);
        Assert.NotNull(returnedModel.CreateDate);
        Assert.NotNull(returnedModel.ModifyDate);

        // Verify database
        var savedPlan = await dbContext.QueryPlans.FirstOrDefaultAsync(q => q.FacilityId == "TestFacility" && q.Type == Frequency.Daily);
        Assert.NotNull(savedPlan);
        Assert.Equal("TestPlan", savedPlan.PlanName);
    }

    [Fact]
    public async Task CreateQueryPlan_InvalidModel_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var model = new QueryPlanApiModel()
        {
            FacilityId = "",
            Type = null
        };

        // Act
        var result = await controller.CreateQueryPlan("TestFacility", model, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        Assert.Equal("Bad Request", ((ProblemDetails)objectResult.Value).Title);
    }

    [Fact]
    public async Task CreateQueryPlan_InvalidQueryOrder_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var model = CreateInvalidOrderQueryPlanApiModel();

        // Act
        var result = await controller.CreateQueryPlan("TestFacility", model, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        Assert.Equal("Incorrect Query Order", ((ProblemDetails)objectResult.Value).Title);
        Assert.Contains("All ReferenceQueryConfig entries must appear after all ParameterQueryConfig entries in InitialQueries", ((ProblemDetails)objectResult.Value).Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateQueryPlan_NullModel_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.CreateQueryPlan("TestFacility", null, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        Assert.Equal("Bad Request", ((ProblemDetails)objectResult.Value).Title);
    }

    [Fact]
    public async Task CreateQueryPlan_InvalidFacilityId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var model = CreateValidQueryPlanApiModel();

        // Act
        var result = await controller.CreateQueryPlan(string.Empty, model, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        Assert.Equal("Bad Request", ((ProblemDetails)objectResult.Value).Title);
    }

    [Fact]
    public async Task CreateQueryPlan_Exception_ReturnsInternalServerError()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var queryPlanManagerMock = new Mock<IQueryPlanManager>();
        queryPlanManagerMock.Setup(m => m.AddAsync(It.IsAny<CreateQueryPlanModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        var logger = new Mock<ILogger<QueryPlanConfigController>>().Object;
        var queryPlanQueries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
        var controller = new QueryPlanConfigController(logger, queryPlanManagerMock.Object, queryPlanQueries);

        var model = CreateValidQueryPlanApiModel();

        // Act
        var result = await controller.CreateQueryPlan("TestFacility", model, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
        Assert.Equal("Internal Server Error", ((ProblemDetails)objectResult.Value).Title);
    }

    [Fact]
    public async Task UpdateQueryPlan_ValidModel_ReturnsAccepted()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed existing query plan
        var existingPlan = new QueryPlan
        {
            FacilityId = "TestFacility",
            Type = Frequency.Daily,
            PlanName = "OldPlan",
            EHRDescription = "OldEHR",
            LookBack = "1d",
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
        dbContext.QueryPlans.Add(existingPlan);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var model = CreateValidQueryPlanApiModel();

        // Act
        var result = await controller.UpdateQueryPlan("TestFacility", model, CancellationToken.None);

        // Assert
        Assert.IsType<AcceptedResult>(result);

        // Verify database
        var updatedPlan = await dbContext.QueryPlans.FirstOrDefaultAsync(q => q.FacilityId == "TestFacility" && q.Type == Frequency.Daily);
        Assert.NotNull(updatedPlan);
        Assert.Equal("TestPlan", updatedPlan.PlanName);
        Assert.Equal("TestEHR", updatedPlan.EHRDescription);
        Assert.Equal("2d", updatedPlan.LookBack);
    }

    [Fact]
    public async Task UpdateQueryPlan_InvalidModel_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var model = new QueryPlanApiModel()
        { 
            FacilityId = "", 
            Type = null 
        };

        // Act
        var result = await controller.UpdateQueryPlan("TestFacility", model, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        Assert.Equal("Bad Request", ((ProblemDetails)objectResult.Value).Title);
    }

    [Fact]
    public async Task UpdateQueryPlan_InvalidQueryOrder_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var model = CreateInvalidOrderQueryPlanApiModel();
        await controller.CreateQueryPlan("TestFacility", CreateValidQueryPlanApiModel(), CancellationToken.None);

        // Act
        var result = await controller.UpdateQueryPlan("TestFacility", model, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        Assert.Equal("Incorrect Query Order", ((ProblemDetails)objectResult.Value).Title);
        Assert.Contains("All ReferenceQueryConfig entries must", ((ProblemDetails)objectResult.Value).Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateQueryPlan_NotFound_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var controller = CreateController(scope);
        var model = CreateValidQueryPlanApiModel();

        // Act
        var result = await controller.UpdateQueryPlan("NonExisting", model, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
        Assert.Equal("Not Found", ((ProblemDetails)objectResult.Value).Title);
        Assert.Contains("A Query Plan was not found for facilityId", ((ProblemDetails)objectResult.Value).Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateQueryPlan_MissingFacilityConfig_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var tenantApiServiceMock = new Mock<ITenantApiService>();
        tenantApiServiceMock.Setup(x => x.GetFacilityConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LantanaGroup.Link.Shared.Application.Models.Tenant.FacilityModel)null);

        var controller = CreateControllerWithTenantMock(scope, tenantApiServiceMock);
        var model = CreateValidQueryPlanApiModel();

        await controller.DeleteQueryPlan("TestFacility", new DeleteQueryPlanParameters() { Type = Frequency.Daily }, CancellationToken.None);

        // Act
        var result = await controller.UpdateQueryPlan("TestFacility", model, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
        Assert.Equal("Not Found", ((ProblemDetails)objectResult.Value).Title);
        Assert.Contains("A Query Plan was not found for facilityId", ((ProblemDetails)objectResult.Value).Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateQueryPlan_NullModel_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.UpdateQueryPlan("TestFacility", null, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        Assert.Equal("Bad Request", ((ProblemDetails)objectResult.Value).Title);
    }

    [Fact]
    public async Task UpdateQueryPlan_InvalidFacilityId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var model = CreateValidQueryPlanApiModel();

        // Act
        var result = await controller.UpdateQueryPlan(string.Empty, model, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        Assert.Equal("Bad Request", ((ProblemDetails)objectResult.Value).Title);
    }

    [Fact]
    public async Task UpdateQueryPlan_Exception_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var queryPlanManagerMock = new Mock<IQueryPlanManager>();
        queryPlanManagerMock.Setup(m => m.UpdateAsync(It.IsAny<UpdateQueryPlanModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        var logger = new Mock<ILogger<QueryPlanConfigController>>().Object;
        var queryPlanQueries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
        var controller = new QueryPlanConfigController(logger, queryPlanManagerMock.Object, queryPlanQueries);

        var model = CreateValidQueryPlanApiModel();

        // Act
        var result = await controller.UpdateQueryPlan("TestFacility", model, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
        Assert.Equal("Not Found", ((ProblemDetails)objectResult.Value).Title);
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
            InitialQueries = new Dictionary<string, IQueryConfig>(),
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };
        dbContext.QueryPlans.Add(queryPlan);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var deleteParams = new DeleteQueryPlanParameters { Type = Frequency.Monthly };

        // Act
        var result = await controller.DeleteQueryPlan("TestFacility", deleteParams, CancellationToken.None);

        // Assert
        Assert.IsType<AcceptedResult>(result);

        // Verify deleted
        var deletedPlan = await dbContext.QueryPlans.FirstOrDefaultAsync(q => q.FacilityId == "TestFacility" && q.Type == Frequency.Monthly);
        Assert.Null(deletedPlan);
    }

    [Fact]
    public async Task DeleteQueryPlan_InvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        controller.ModelState.AddModelError("Type", "Invalid");
        var deleteParams = new DeleteQueryPlanParameters { Type = Frequency.Daily };

        // Act
        var result = await controller.DeleteQueryPlan("TestFacility", deleteParams, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
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
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        Assert.Equal("Bad Request", ((ProblemDetails)objectResult.Value).Title);
        Assert.Contains("type query parameter must be defined", ((ProblemDetails)objectResult.Value).Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteQueryPlan_MissingFacilityId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var deleteParams = new DeleteQueryPlanParameters { Type = Frequency.Monthly };

        // Act
        var result = await controller.DeleteQueryPlan(string.Empty, deleteParams, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        Assert.Equal("Bad Request", ((ProblemDetails)objectResult.Value).Title);
        Assert.Contains("facilityId is required", ((ProblemDetails)objectResult.Value).Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteQueryPlan_NonExisting_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var controller = CreateController(scope);
        var deleteParams = new DeleteQueryPlanParameters { Type = Frequency.Monthly };

        // Act
        var result = await controller.DeleteQueryPlan("NonExistingFacility", deleteParams, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
        Assert.Equal("Not Found", ((ProblemDetails)objectResult.Value).Title);
        Assert.Contains("A QueryPlan or Query component was not found", ((ProblemDetails)objectResult.Value).Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteQueryPlan_Exception_ReturnsInternalServerError()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var queryPlanManagerMock = new Mock<IQueryPlanManager>();
        queryPlanManagerMock.Setup(m => m.DeleteAsync(It.IsAny<string>(), It.IsAny<Frequency>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        var logger = new Mock<ILogger<QueryPlanConfigController>>().Object;
        var queryPlanQueries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
        var controller = new QueryPlanConfigController(logger, queryPlanManagerMock.Object, queryPlanQueries);

        var deleteParams = new DeleteQueryPlanParameters { Type = Frequency.Daily };

        // Act
        var result = await controller.DeleteQueryPlan("TestFacility", deleteParams, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
        Assert.Equal("Internal Server Error", ((ProblemDetails)objectResult.Value).Title);
    }

    private QueryPlanApiModel CreateValidQueryPlanApiModel()
    {
        return new QueryPlanApiModel
        {
            PlanName = "TestPlan",
            FacilityId = "TestFacility",
            Type = Frequency.Daily,
            EHRDescription = "TestEHR",
            LookBack = "2d",
            InitialQueries = new Dictionary<string, IQueryConfig>
            {
                { "1", new ParameterQueryConfig { ResourceType = "Patient", Parameters = new List<IParameter> { new LiteralParameter { Name = "id", Literal = "123" } } } }
            },
            SupplementalQueries = new Dictionary<string, IQueryConfig>
            {
                { "1", new ReferenceQueryConfig { ResourceType = "Encounter" } }
            }
        };
    }

    private QueryPlanApiModel CreateInvalidOrderQueryPlanApiModel()
    {
        return new QueryPlanApiModel
        {
            PlanName = "InvalidPlan",
            FacilityId = "TestFacility",
            Type = Frequency.Daily,
            EHRDescription = "TestEHR",
            LookBack = "2d",
            InitialQueries = new Dictionary<string, IQueryConfig>
            {
                { "1", new ReferenceQueryConfig { ResourceType = "Encounter" } },
                { "2", new ParameterQueryConfig { ResourceType = "Patient", Parameters = new List<IParameter> { new LiteralParameter { Name = "id", Literal = "123" } } } }
            },
            SupplementalQueries = new Dictionary<string, IQueryConfig>
            {
                { "1", new ReferenceQueryConfig { ResourceType = "Condition" } }
            }
        };
    }
}