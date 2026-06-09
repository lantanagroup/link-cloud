using DataAcquisition.Domain.Application.Models;
using DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Extensions;
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
            var mocker = new AutoMocker();
            mocker.GetMock<IQueryPlanQueries>().Setup(x => x.GetAsync(It.IsAny<string>(), Frequency.Monthly, CancellationToken.None))
                .ReturnsAsync((QueryPlanModel?)null);

            var controller = mocker.CreateInstance<QueryPlanConfigController>();

            var result = await controller.GetQueryPlan(facilityId, new GetQueryPlanParameters { Type = Frequency.Monthly }, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal((int)HttpStatusCode.NotFound, problem.StatusCode.Value);
        }

        [Fact]
        public async Task GetQueryPlanNegativeTest_InvalidFacilityId()
        {
            var mocker = new AutoMocker();
            var controller = mocker.CreateInstance<QueryPlanConfigController>();

            // Unit tests do not run validation
            controller.ModelState.AddModelError("facilityId", "facilityId is required");

            var result = await controller.GetQueryPlan("", new GetQueryPlanParameters { Type = Frequency.Monthly }, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal((int)HttpStatusCode.BadRequest, problem.StatusCode.Value);
        }

        [Fact]
        public async Task CreateQueryPlanTest()
        {
            var facilityId = "test-facility-id";
            var mocker = new AutoMocker();
            mocker.GetMock<IQueryPlanManager>().Setup(x => x.AddAsync(It.IsAny<CreateQueryPlanModel>(), CancellationToken.None))
                .ReturnsAsync(new QueryPlanModel());

            var controller = mocker.CreateInstance<QueryPlanConfigController>();

            var result = await controller.CreateQueryPlan(facilityId,
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
        public async Task UpdateQueryPlanTest()
        {
            var facilityId = "test-facility-id";
            var mocker = new AutoMocker();
            mocker.GetMock<IQueryPlanQueries>().Setup(x => x.ExistsAsync(It.IsAny<string>(), Frequency.Monthly, CancellationToken.None))
                .ReturnsAsync(true);
            mocker.GetMock<IQueryPlanQueries>().Setup(x => x.GetAsync(It.IsAny<string>(), Frequency.Monthly, CancellationToken.None))
                .ReturnsAsync(new QueryPlanModel());
            mocker.GetMock<IQueryPlanManager>().Setup(x => x.UpdateAsync(It.IsAny<UpdateQueryPlanModel>(), CancellationToken.None))
                .ReturnsAsync(new QueryPlanModel());

            var controller = mocker.CreateInstance<QueryPlanConfigController>();

            var result = await controller.UpdateQueryPlan(facilityId,
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
        public async Task DeleteQueryPlanTest()
        {
            var facilityId = "test-facility-id";
            var mocker = new AutoMocker();

            var queryPlan = new QueryPlanModel { FacilityId = facilityId, Type = Frequency.Monthly };
            mocker.GetMock<IQueryPlanManager>().Setup(x => x.AddAsync(It.IsAny<CreateQueryPlanModel>(), CancellationToken.None))
                .ReturnsAsync(queryPlan);

            mocker.GetMock<IQueryPlanQueries>().Setup(x => x.ExistsAsync(It.IsAny<string>(), Frequency.Monthly, CancellationToken.None))
                .ReturnsAsync(true);

            var createController = mocker.CreateInstance<QueryPlanConfigController>();

            await createController.CreateQueryPlan(facilityId, new QueryPlanApiModel
            {
                FacilityId = facilityId,
                Type = Frequency.Monthly,
                PlanName = "Test",
                InitialQueries = new Dictionary<string, IQueryConfig> { { "1", new ParameterQueryConfig { Parameters = new List<IParameter> { } } } },
                SupplementalQueries = new Dictionary<string, IQueryConfig> { { "1", new ParameterQueryConfig { Parameters = new List<IParameter> { } } } },
            }, CancellationToken.None);

            mocker.GetMock<IQueryPlanManager>().Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<Frequency>(), CancellationToken.None));

            var controller = mocker.CreateInstance<QueryPlanConfigController>();

            var result = await controller.DeleteQueryPlan(facilityId, new DeleteQueryPlanParameters { Type = Frequency.Monthly }, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal((int)HttpStatusCode.Accepted, problem.StatusCode!.Value);
        }

        [Fact]
        public async Task DeleteQueryPlanNegativeTest_InvalidFacilityId()
        {
            var mocker = new AutoMocker();
            var controller = mocker.CreateInstance<QueryPlanConfigController>();

            var result = await controller.DeleteQueryPlan("", new DeleteQueryPlanParameters { Type = Frequency.Monthly }, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal((int)HttpStatusCode.BadRequest, problem.StatusCode.Value);
        }

        [Fact]
        public async Task DeleteQueryPlanNegativeTest_NullResult()
        {
            var facilityId = "test-facility-id";
            var mocker = new AutoMocker();
            mocker.GetMock<IQueryPlanManager>().Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<Frequency>(), CancellationToken.None))
                .ThrowsAsync(new NullReferenceException("Not Found"));

            var controller = mocker.CreateInstance<QueryPlanConfigController>();

            var result = await controller.DeleteQueryPlan(facilityId, new DeleteQueryPlanParameters { Type = Frequency.Monthly }, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal((int)HttpStatusCode.NotFound, problem.StatusCode.Value);
        }

        // ==================== Additional negative-path tests ====================

        [Fact]
        public async Task GetQueryPlan_EmptyFacilityId_ReturnsErrorsWithFacilityIdError()
        {
            var mocker = new AutoMocker();
            var controller = mocker.CreateInstance<QueryPlanConfigController>();

            var result = await controller.GetQueryPlan(" ", new GetQueryPlanParameters(), CancellationToken.None);

            var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
            var problemDetailsResult = Assert.IsAssignableFrom<ValidationProblemDetails>(objectResult.Value);

            Assert.Equal("https://datatracker.ietf.org/doc/html/rfc9457#section-3", problemDetailsResult.Type);
            Assert.Equal("Bad Request", problemDetailsResult.Title);

            Assert.Contains(problemDetailsResult.Errors, p =>
                p.Key == "facilityId" &&
                p.Value.Any(v => v.Contains("parameter facilityId is required", StringComparison.OrdinalIgnoreCase)));
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
