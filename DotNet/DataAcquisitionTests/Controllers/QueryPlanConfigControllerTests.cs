using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using System.Net;

namespace DataAcquisitionUnitTests.Controllers
{
    public class QueryPlanConfigControllerTests
    {
        private AutoMocker? _mocker;
        private const string facilityId = "testFacilityId";

        [Fact]
        public async void GetQueryPlanTest()
        {
            var cancellationToken = new CancellationToken();
            _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.GetAsync(It.IsAny<string>(), Frequency.Monthly, CancellationToken.None))
                .ReturnsAsync(new QueryPlan());

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.GetQueryPlan(facilityId, Frequency.Monthly, CancellationToken.None);
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async void GetQueryPlanNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
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
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.GetQueryPlan("", Frequency.Monthly,  CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void CreateQueryPlanTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.AddAsync(It.IsAny<QueryPlan>(), CancellationToken.None))
                .ReturnsAsync(new QueryPlan());

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.CreateQueryPlan(facilityId, new QueryPlan(), CancellationToken.None);
            Assert.IsType<CreatedAtActionResult>(result);
        }

        [Fact]
        public async void CreateQueryPlanNegativeTest_NullContent()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.CreateQueryPlan(facilityId, null, CancellationToken.None);
            
            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void UpdateQueryPlanTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.GetAsync(It.IsAny<string>(), Frequency.Monthly,  CancellationToken.None))
                .ReturnsAsync(new QueryPlan());
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.UpdateAsync(It.IsAny<QueryPlan>(),  CancellationToken.None))
                .ReturnsAsync(new QueryPlan());

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.UpdateQueryPlan(facilityId, new QueryPlan(), CancellationToken.None);
            Assert.IsType<ObjectResult>(result);
        }

        [Fact]
        public async void UpdateQueryPlanNegativeTest_NullBody()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.GetAsync(It.IsAny<string>(), Frequency.Monthly, CancellationToken.None))
                .ReturnsAsync(new QueryPlan());
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.UpdateAsync(It.IsAny<QueryPlan?>(), CancellationToken.None))
                .ReturnsAsync(new QueryPlan());

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.UpdateQueryPlan(facilityId, (QueryPlan?)null, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void DeleteQueryPlanTest()
        {
            _mocker = new AutoMocker();

            var queryPlan = new QueryPlan() { FacilityId = facilityId };
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.AddAsync(It.IsAny<QueryPlan>(), CancellationToken.None))
                .ReturnsAsync(queryPlan);

            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.GetAsync(It.IsAny<string>(), Frequency.Monthly, CancellationToken.None))
                .ReturnsAsync(queryPlan);

            var _createController = _mocker.CreateInstance<QueryPlanConfigController>();

            var createResult = await _createController.CreateQueryPlan(facilityId, queryPlan, CancellationToken.None);

            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.DeleteAsync(It.IsAny<string>(), CancellationToken.None));

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.DeleteQueryPlan(facilityId, Frequency.Monthly, CancellationToken.None);
            
            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.Accepted);
        }

        [Fact]
        public async void DeleteQueryPlanNegativeTest_InvalidFacilityId()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.DeleteQueryPlan("", Frequency.Monthly, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void DeleteQueryPlanNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IQueryPlanManager>().Setup(x => x.DeleteAsync(It.IsAny<string>(), CancellationToken.None))
                .ThrowsAsync(new NullReferenceException("Not Found"));

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.DeleteQueryPlan(facilityId, Frequency.Monthly, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.NotFound);
        }
    }
}
