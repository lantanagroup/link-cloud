using DataAcquisition.Domain.Application.Models;
using DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using System.Net;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Controllers
{
    [Trait("Category", "UnitTests")]
    public class QueryPlanConfigControllerTests
    {
        [Fact]
        public async Task GetQueryPlanNegativeTest_NullResult()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanQueries>().Setup(x => x.GetAsync(It.IsAny<string>(), Frequency.Monthly, CancellationToken.None))
                .ReturnsAsync((QueryPlanModel?)null);

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.GetQueryPlan(facilityId, new GetQueryPlanParameters { Type = Frequency.Monthly }, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal((int)HttpStatusCode.NotFound, problem.StatusCode.Value);
        }

        [Fact]
        public async Task GetQueryPlanNegativeTest_InvalidFacilityId()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.GetQueryPlan("", new GetQueryPlanParameters { Type = Frequency.Monthly }, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal((int)HttpStatusCode.BadRequest, problem.StatusCode.Value);
        }

        [Fact]
        public async Task CreateQueryPlanTest()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.AddAsync(It.IsAny<CreateQueryPlanModel>(), CancellationToken.None))
                .ReturnsAsync(new QueryPlanModel());

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.CreateQueryPlan(facilityId,
                new QueryPlanApiModel
                {
                    FacilityId = facilityId,
                    Type = Frequency.Monthly,
                    PlanName = "Test",
                    InitialQueries = new Dictionary<string, IQueryConfig> { { "1", new ParameterQueryConfig { Parameters = new List<IParameter> { } } } },
                    SupplementalQueries = new Dictionary<string, IQueryConfig> { { "1", new ParameterQueryConfig { Parameters = new List<IParameter> { } } } },
                }, CancellationToken.None);
            Assert.IsType<CreatedAtActionResult>(result);
        }

        [Fact]
        public async Task CreateQueryPlanNegativeTest_NullContent()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.CreateQueryPlan(facilityId, null, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal((int)HttpStatusCode.BadRequest, problem.StatusCode.Value);
        }

        [Fact]
        public async Task UpdateQueryPlanTest()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanQueries>().Setup(x => x.ExistsAsync(It.IsAny<string>(), Frequency.Monthly, CancellationToken.None))
                .ReturnsAsync(true);
            _mocker.GetMock<IQueryPlanQueries>().Setup(x => x.GetAsync(It.IsAny<string>(), Frequency.Monthly, CancellationToken.None))
                .ReturnsAsync(new QueryPlanModel());
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.UpdateAsync(It.IsAny<UpdateQueryPlanModel>(), CancellationToken.None))
                .ReturnsAsync(new QueryPlanModel());

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.UpdateQueryPlan(facilityId,
                new QueryPlanApiModel
                {
                    FacilityId = facilityId,
                    Type = Frequency.Monthly,
                    PlanName = "Test",
                    InitialQueries = new Dictionary<string, IQueryConfig> { { "1", new ParameterQueryConfig { Parameters = new List<IParameter> { } } } },
                    SupplementalQueries = new Dictionary<string, IQueryConfig> { { "1", new ParameterQueryConfig { Parameters = new List<IParameter> { } } } },
                }, CancellationToken.None);
            var obj = Assert.IsType<AcceptedResult>(result);
            Assert.Equal((int)HttpStatusCode.Accepted, obj.StatusCode);
        }

        [Fact]
        public async Task UpdateQueryPlanNegativeTest_NullBody()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanQueries>().Setup(x => x.GetAsync(It.IsAny<string>(), Frequency.Monthly, CancellationToken.None))
                .ReturnsAsync(new QueryPlanModel());
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.UpdateAsync(It.IsAny<UpdateQueryPlanModel?>(), CancellationToken.None))
                .ReturnsAsync(new QueryPlanModel());

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.UpdateQueryPlan(facilityId, null, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal((int)HttpStatusCode.BadRequest, problem.StatusCode.Value);
        }

        [Fact]
        public async Task DeleteQueryPlanTest()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();

            var queryPlan = new QueryPlanModel { FacilityId = facilityId, Type = Frequency.Monthly };
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.AddAsync(It.IsAny<CreateQueryPlanModel>(), CancellationToken.None))
                .ReturnsAsync(queryPlan);

            _mocker.GetMock<IQueryPlanQueries>().Setup(x => x.ExistsAsync(It.IsAny<string>(), Frequency.Monthly, CancellationToken.None))
                .ReturnsAsync(true);

            var _createController = _mocker.CreateInstance<QueryPlanConfigController>();

            await _createController.CreateQueryPlan(facilityId, new QueryPlanApiModel
            {
                FacilityId = facilityId,
                Type = Frequency.Monthly,
                PlanName = "Test",
                InitialQueries = new Dictionary<string, IQueryConfig> { { "1", new ParameterQueryConfig { Parameters = new List<IParameter> { } } } },
                SupplementalQueries = new Dictionary<string, IQueryConfig> { { "1", new ParameterQueryConfig { Parameters = new List<IParameter> { } } } },
            }, CancellationToken.None);

            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<Frequency>(), CancellationToken.None));

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.DeleteQueryPlan(facilityId, new DeleteQueryPlanParameters { Type = Frequency.Monthly }, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal((int)HttpStatusCode.Accepted, problem.StatusCode!.Value);
        }

        [Fact]
        public async Task DeleteQueryPlanNegativeTest_InvalidFacilityId()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.DeleteQueryPlan("", new DeleteQueryPlanParameters { Type = Frequency.Monthly }, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal((int)HttpStatusCode.BadRequest, problem.StatusCode.Value);
        }

        [Fact]
        public async Task DeleteQueryPlanNegativeTest_NullResult()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<Frequency>(), CancellationToken.None))
                .ThrowsAsync(new NullReferenceException("Not Found"));

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.DeleteQueryPlan(facilityId, new DeleteQueryPlanParameters { Type = Frequency.Monthly }, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal((int)HttpStatusCode.NotFound, problem.StatusCode.Value);
        }

        // ==================== Additional negative-path tests ====================

        [Fact]
        public async Task GetQueryPlan_MissingType_ReturnsBadRequest()
        {
            var _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.GetQueryPlan("test-facility-id", new GetQueryPlanParameters(), CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
            Assert.Contains("type query parameter must be defined", ((ProblemDetails)objectResult.Value!).Detail!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetQueryPlan_InvalidModelState_ReturnsBadRequest()
        {
            var _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();
            _controller.ModelState.AddModelError("Type", "Invalid");

            var result = await _controller.GetQueryPlan("test-facility-id", new GetQueryPlanParameters { Type = Frequency.Daily }, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetQueryPlan_Exception_ReturnsInternalServerError()
        {
            var _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanQueries>()
                .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<Frequency>(), CancellationToken.None))
                .ThrowsAsync(new Exception("boom"));

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.GetQueryPlan("test-facility-id", new GetQueryPlanParameters { Type = Frequency.Daily }, CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
        }

        [Fact]
        public async Task CreateQueryPlan_InvalidFacilityId_ReturnsBadRequest()
        {
            var _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.CreateQueryPlan(string.Empty,
                new QueryPlanApiModel { FacilityId = "test", Type = Frequency.Daily },
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        }

        [Fact]
        public async Task CreateQueryPlan_MissingType_ReturnsBadRequest()
        {
            var _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.CreateQueryPlan("test-facility-id",
                new QueryPlanApiModel { FacilityId = "test", Type = null },
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        }

        [Fact]
        public async Task CreateQueryPlan_InvalidQueryOrder_ReturnsBadRequest()
        {
            var _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanQueries>()
                .Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<Frequency>(), CancellationToken.None))
                .ReturnsAsync(false);
            _mocker.GetMock<IQueryPlanManager>()
                .Setup(x => x.AddAsync(It.IsAny<CreateQueryPlanModel>(), CancellationToken.None))
                .ThrowsAsync(new IncorrectQueryPlanOrderException("Query Plan validation failed: Reference query before parameter query."));

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.CreateQueryPlan("test-facility-id",
                new QueryPlanApiModel
                {
                    FacilityId = "test-facility-id",
                    Type = Frequency.Daily,
                    PlanName = "Plan",
                    InitialQueries = new Dictionary<string, IQueryConfig> { { "1", new ReferenceQueryConfig() } },
                    SupplementalQueries = new Dictionary<string, IQueryConfig>()
                },
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
            Assert.Contains("Query Plan validation failed:", ((ProblemDetails)objectResult.Value!).Detail!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateQueryPlan_AlreadyExists_ReturnsConflict()
        {
            var _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanQueries>()
                .Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<Frequency>(), CancellationToken.None))
                .ReturnsAsync(true);

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.CreateQueryPlan("test-facility-id",
                new QueryPlanApiModel { FacilityId = "test", Type = Frequency.Daily, PlanName = "P" },
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal((int)HttpStatusCode.Conflict, objectResult.StatusCode);
        }

        [Fact]
        public async Task CreateQueryPlan_Exception_ReturnsInternalServerError()
        {
            var _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanQueries>()
                .Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<Frequency>(), CancellationToken.None))
                .ReturnsAsync(false);
            _mocker.GetMock<IQueryPlanManager>()
                .Setup(x => x.AddAsync(It.IsAny<CreateQueryPlanModel>(), CancellationToken.None))
                .ThrowsAsync(new Exception("boom"));

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.CreateQueryPlan("test-facility-id",
                new QueryPlanApiModel { FacilityId = "test", Type = Frequency.Daily, PlanName = "P" },
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
        }

        [Fact]
        public async Task UpdateQueryPlan_InvalidFacilityId_ReturnsBadRequest()
        {
            var _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.UpdateQueryPlan(string.Empty,
                new QueryPlanApiModel { FacilityId = "x", Type = Frequency.Daily },
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        }

        [Fact]
        public async Task UpdateQueryPlan_MissingType_ReturnsBadRequest()
        {
            var _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.UpdateQueryPlan("test-facility-id",
                new QueryPlanApiModel { FacilityId = "x", Type = null },
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        }

        [Fact]
        public async Task UpdateQueryPlan_NotFound_ReturnsNotFound()
        {
            var _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanQueries>()
                .Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<Frequency>(), CancellationToken.None))
                .ReturnsAsync(false);

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.UpdateQueryPlan("test-facility-id",
                new QueryPlanApiModel { FacilityId = "test-facility-id", Type = Frequency.Daily, PlanName = "P" },
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
        }

        [Fact]
        public async Task UpdateQueryPlan_InvalidQueryOrder_ReturnsBadRequest()
        {
            var _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanQueries>()
                .Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<Frequency>(), CancellationToken.None))
                .ReturnsAsync(true);
            _mocker.GetMock<IQueryPlanManager>()
                .Setup(x => x.UpdateAsync(It.IsAny<UpdateQueryPlanModel>(), CancellationToken.None))
                .ThrowsAsync(new IncorrectQueryPlanOrderException("Query Plan validation failed: Query Plan order is invalid."));

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.UpdateQueryPlan("test-facility-id",
                new QueryPlanApiModel { FacilityId = "test-facility-id", Type = Frequency.Daily, PlanName = "P" },
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
            Assert.Contains("Query Plan validation failed: Query Plan", ((ProblemDetails)objectResult.Value!).Detail!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateQueryPlan_GenericException_ReturnsInternalServerError()
        {
            var _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanQueries>()
                .Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<Frequency>(), CancellationToken.None))
                .ReturnsAsync(true);
            _mocker.GetMock<IQueryPlanManager>()
                .Setup(x => x.UpdateAsync(It.IsAny<UpdateQueryPlanModel>(), CancellationToken.None))
                .ThrowsAsync(new Exception("boom"));

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.UpdateQueryPlan("test-facility-id",
                new QueryPlanApiModel { FacilityId = "test-facility-id", Type = Frequency.Daily, PlanName = "P" },
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
        }

        [Fact]
        public async Task DeleteQueryPlan_InvalidModelState_ReturnsBadRequest()
        {
            var _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();
            _controller.ModelState.AddModelError("Type", "Invalid");

            var result = await _controller.DeleteQueryPlan("test-facility-id", new DeleteQueryPlanParameters { Type = Frequency.Daily }, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task DeleteQueryPlan_MissingType_ReturnsBadRequest()
        {
            var _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.DeleteQueryPlan("test-facility-id", new DeleteQueryPlanParameters(), CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
            Assert.Contains("type query parameter must be defined", ((ProblemDetails)objectResult.Value!).Detail!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteQueryPlan_Exception_ReturnsInternalServerError()
        {
            var _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanQueries>()
                .Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<Frequency>(), CancellationToken.None))
                .ReturnsAsync(true);
            _mocker.GetMock<IQueryPlanManager>()
                .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<Frequency>(), CancellationToken.None))
                .ThrowsAsync(new Exception("boom"));

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.DeleteQueryPlan("test-facility-id", new DeleteQueryPlanParameters { Type = Frequency.Daily }, CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
        }
    }
}
