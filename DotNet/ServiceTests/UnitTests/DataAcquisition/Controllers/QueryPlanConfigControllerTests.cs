using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using System.Net;

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

            var result = await _controller.GetQueryPlan(facilityId, Frequency.Monthly, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async void GetQueryPlanNegativeTest_InvalidFacilityId()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.GetQueryPlan("", Frequency.Monthly, CancellationToken.None);

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

            var result = await _controller.CreateQueryPlan(facilityId, new QueryPlan(), CancellationToken.None);
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

            var result = await _controller.UpdateQueryPlan(facilityId, new QueryPlan(), CancellationToken.None);
            Assert.IsType<ObjectResult>(result);
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

            var queryPlan = new QueryPlan() { FacilityId = facilityId };
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.AddAsync(It.IsAny<QueryPlan>(), CancellationToken.None))
                .ReturnsAsync(queryPlan);

            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.GetAsync(It.IsAny<string>(), Frequency.Monthly, CancellationToken.None))
                .ReturnsAsync(queryPlan);

            var _createController = _mocker.CreateInstance<QueryPlanConfigController>();

            var createResult = await _createController.CreateQueryPlan(facilityId, queryPlan, CancellationToken.None);

            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<Frequency>(), CancellationToken.None));

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.DeleteQueryPlan(facilityId, Frequency.Monthly, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.Accepted);
        }

        [Fact]
        public async void DeleteQueryPlanNegativeTest_InvalidFacilityId()
        {
            var facilityId = "test-facility-id";
            var _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.DeleteQueryPlan("", Frequency.Monthly, CancellationToken.None);

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

            var result = await _controller.DeleteQueryPlan(facilityId, Frequency.Monthly, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.NotFound);
        }
    }
}
