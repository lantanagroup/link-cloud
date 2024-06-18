using LantanaGroup.Link.DataAcquisition.Application.Commands.Config;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Entities;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DataAcquisitionUnitTests.Controllers
{
    public class QueryConfigControllerTests
    {
        private AutoMocker? _mocker;
        private const string facilityId = "testFacilityId";

        [Fact]
        public async void GetFhirConfigurationTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<GetFhirQueryConfigQuery>(), CancellationToken.None))
                .ReturnsAsync(new FhirQueryConfiguration());

            var _controller = _mocker.CreateInstance<QueryConfigController>();

            var result = await _controller.GetFhirConfiguration(facilityId, CancellationToken.None);
            Assert.IsType<OkObjectResult>(result.Result);
        }

        [Fact]
        public async void GetFhirConfigurationNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<GetFhirQueryConfigQuery>(), CancellationToken.None))
                .ReturnsAsync((FhirQueryConfiguration)null);

            var _controller = _mocker.CreateInstance<QueryConfigController>();

            var result = await _controller.GetFhirConfiguration(facilityId, CancellationToken.None);

            Assert.True(result.Value == null);
            Assert.IsType<ObjectResult>(result.Result);
            var objectResult = (ObjectResult)result.Result;
            Assert.True(((ProblemDetails)objectResult.Value).Status == (int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async void GetFhirConfigurationNegativeTest_InvalidFacilityId()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryConfigController>();

            var result = await _controller.GetFhirConfiguration("", CancellationToken.None);

            Assert.True(result.Value == null);
            Assert.IsType<ObjectResult>(result.Result);
            var objectResult = (ObjectResult)result.Result;
            Assert.True(((ProblemDetails)objectResult.Value).Status == (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void CreateFhirConfigurationTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<SaveFhirQueryConfigCommand>(), CancellationToken.None))
                .ReturnsAsync(new FhirQueryConfiguration());

            var _controller = _mocker.CreateInstance<QueryConfigController>();

            var result = _controller.CreateFhirConfiguration(new FhirQueryConfiguration(), CancellationToken.None).Result;

            Assert.IsType<ActionResult<FhirQueryConfiguration>>(result);
            Assert.NotNull(((CreatedAtActionResult)result.Result).Value);
        }

        [Fact]
        public async void CreateFhirConfigurationNegativeTest_NullBody()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryConfigController>();

            var result = await _controller.CreateFhirConfiguration(null, CancellationToken.None);

            Assert.True(result.Value == null);
            Assert.IsType<ObjectResult>(result.Result);
            var objectResult = (ObjectResult)result.Result;
            Assert.True(((ProblemDetails)objectResult.Value).Status == (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void UpdateFhirConfigurationTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<GetFhirQueryConfigQuery>(), CancellationToken.None))
                .ReturnsAsync(new FhirQueryConfiguration());
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<SaveFhirQueryConfigCommand>(), CancellationToken.None))
                .ReturnsAsync(new FhirQueryConfiguration());

            var _controller = _mocker.CreateInstance<QueryConfigController>();

            var result = await _controller.UpdateFhirConfiguration(new FhirQueryConfiguration(), CancellationToken.None);
            Assert.IsType<AcceptedResult>(result);
        }

        [Fact]
        public async void UpdateFhirConfigurationNegativeTest_NullBody()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryConfigController>();

            var result = await _controller.UpdateFhirConfiguration(null, CancellationToken.None);

            Assert.IsType<ObjectResult>(result);
            var objectResult = (ObjectResult)result;
            Assert.NotNull(objectResult.Value);
            Assert.True(((ProblemDetails)objectResult.Value).Status == (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void DeleteFhirConfigurationTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<DeleteFhirQueryConfigurationCommand>(), CancellationToken.None))
                .ReturnsAsync(Unit.Value);

            var _controller = _mocker.CreateInstance<QueryConfigController>();

            var result = await _controller.DeleteFhirConfiguration(facilityId, CancellationToken.None);
            Assert.IsType<AcceptedResult>(result);
        }

        [Fact]
        public async void DeleteFhirConfigurationNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<DeleteFhirQueryConfigurationCommand>(), CancellationToken.None))
                .Throws(new Exception());

            var _controller = _mocker.CreateInstance<QueryConfigController>();

            try
            {
                var result = await _controller.DeleteFhirConfiguration(facilityId, CancellationToken.None);
                Assert.True(false);
            }
            catch (Exception)
            {
                Assert.True(true);
            }
        }

        [Fact]
        public async void DeleteFhirConfigurationNegativeTest_InvalidFacilityId()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryConfigController>();

            var result = await _controller.DeleteFhirConfiguration("", CancellationToken.None);

            Assert.IsType<ObjectResult>(result);
            var objectResult = (ObjectResult)result;
            Assert.NotNull(objectResult.Value);
            Assert.True(((ProblemDetails)objectResult.Value).Status == (int)HttpStatusCode.BadRequest);
        }
    }
}
