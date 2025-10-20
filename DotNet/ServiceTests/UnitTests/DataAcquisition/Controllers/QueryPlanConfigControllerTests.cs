using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
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
        public async void GetQueryPlanNegativeTest_NullResult()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.GetAsync(It.IsAny<string>(), Frequency.Monthly, CancellationToken.None))
                .ReturnsAsync((QueryPlan?)null);

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.GetQueryPlan(facilityId, new GetQueryPlanParameters { Type = Frequency.Monthly }, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async void GetQueryPlanNegativeTest_InvalidFacilityId()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.GetQueryPlan("", new GetQueryPlanParameters { Type = Frequency.Monthly }, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void CreateQueryPlanTest()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.AddAsync(It.IsAny<QueryPlan>(), CancellationToken.None))
                .ReturnsAsync(new QueryPlan());

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.CreateQueryPlan(facilityId, 
                new QueryPlanPostModel
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
        public async void CreateQueryPlanNegativeTest_NullContent()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.CreateQueryPlan(facilityId, null, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void UpdateQueryPlanTest()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.GetAsync(It.IsAny<string>(), Frequency.Monthly, CancellationToken.None))
                .ReturnsAsync(new QueryPlan());
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.UpdateAsync(It.IsAny<QueryPlan>(), CancellationToken.None))
                .ReturnsAsync(new QueryPlan());

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.UpdateQueryPlan(facilityId, 
                new QueryPlanPutModel 
                { 
                    Id = Guid.NewGuid().ToString(), 
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
        public async void UpdateQueryPlanNegativeTest_NullBody()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.GetAsync(It.IsAny<string>(), Frequency.Monthly, CancellationToken.None))
                .ReturnsAsync(new QueryPlan());
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.UpdateAsync(It.IsAny<QueryPlan?>(), CancellationToken.None))
                .ReturnsAsync(new QueryPlan());

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.UpdateQueryPlan(facilityId, null, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void DeleteQueryPlanTest()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();

            var queryPlan = new QueryPlan { FacilityId = facilityId, Type = Frequency.Monthly };
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.AddAsync(It.IsAny<QueryPlan>(), CancellationToken.None))
                .Returns(Task.FromResult(queryPlan));

            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.GetAsync(It.IsAny<string>(), Frequency.Monthly, CancellationToken.None))
                .Returns(Task.FromResult(queryPlan));

            var _createController = _mocker.CreateInstance<QueryPlanConfigController>();

            await _createController.CreateQueryPlan(facilityId, new QueryPlanPostModel 
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
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.Accepted);
        }

        [Fact]
        public async void DeleteQueryPlanNegativeTest_InvalidFacilityId()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.DeleteQueryPlan("", new DeleteQueryPlanParameters { Type = Frequency.Monthly }, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void DeleteQueryPlanNegativeTest_NullResult()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<Frequency>(), CancellationToken.None))
                .ThrowsAsync(new NullReferenceException("Not Found"));

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.DeleteQueryPlan(facilityId, new DeleteQueryPlanParameters { Type = Frequency.Monthly }, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.NotFound);
        }
    }
}
