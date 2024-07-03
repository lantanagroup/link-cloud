using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using MediatR;
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
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<GetQueryPlanQuery>(), CancellationToken.None))
                .ReturnsAsync(new QueryPlan());

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.GetQueryPlan(facilityId, CancellationToken.None);
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async void GetQueryPlanNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<GetQueryPlanQuery>(), CancellationToken.None))
                .ReturnsAsync((QueryPlan?)null);

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.GetQueryPlan(facilityId, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async void GetQueryPlanNegativeTest_InvalidFacilityId()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.GetQueryPlan("",   CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void CreateQueryPlanTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<SaveQueryPlanCommand>(), CancellationToken.None))
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
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<GetQueryPlanQuery>(), CancellationToken.None))
                .ReturnsAsync(new QueryPlan());
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<SaveQueryPlanCommand>(), CancellationToken.None))
                .ReturnsAsync(new QueryPlan());

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.UpdateQueryPlan(facilityId, new QueryPlan(), CancellationToken.None);
            Assert.IsType<AcceptedResult>(result);
        }

        [Fact]
        public async void UpdateQueryPlanNegativeTest_NullBody()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<GetQueryPlanQuery>(), CancellationToken.None))
                .ReturnsAsync(new QueryPlan());
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<SaveQueryPlanCommand>(), CancellationToken.None))
                .ReturnsAsync(new QueryPlan());

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.UpdateQueryPlan(facilityId, null, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void DeleteQueryPlanTest()
        {
            _mocker = new AutoMocker();

            var queryPlan = new QueryPlan() { FacilityId = facilityId };
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<SaveQueryPlanCommand>(), CancellationToken.None))
                .ReturnsAsync(queryPlan);

            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<GetQueryPlanQuery>(), CancellationToken.None))
                .ReturnsAsync(queryPlan);

            var _createController = _mocker.CreateInstance<QueryPlanConfigController>();

            var createResult = await _createController.CreateQueryPlan(facilityId, queryPlan, CancellationToken.None);

            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<DeleteQueryPlanCommand>(), CancellationToken.None))
                .ReturnsAsync(Unit.Value);

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.DeleteQueryPlan(facilityId, CancellationToken.None);
            
            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.Accepted);
        }

        [Fact]
        public async void DeleteQueryPlanNegativeTest_InvalidFacilityId()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.DeleteQueryPlan("", CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void DeleteQueryPlanNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<DeleteQueryPlanCommand>(), CancellationToken.None))
                .ThrowsAsync(new NullReferenceException("Not Found"));

            var _controller = _mocker.CreateInstance<QueryPlanConfigController>();

            var result = await _controller.DeleteQueryPlan(facilityId, CancellationToken.None);

            var problem = (ObjectResult)result;
            Assert.Equal(problem.StatusCode.Value, (int)HttpStatusCode.NotFound);
        }
    }
}
