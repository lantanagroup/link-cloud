using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using System.Net;

namespace UnitTests.DataAcquisition.Controllers
{
    [Trait("Category", "UnitTests")]
    public class QueryConfigControllerTests
    {
        private AutoMocker? _mocker;
        private const string facilityId = "testFacilityId";

        [Fact]
        public async void GetFhirConfigurationTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IFhirQueryConfigurationManager>().Setup(x => x.GetAsync(It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync(new FhirQueryConfiguration());

            var _controller = _mocker.CreateInstance<QueryConfigController>();

            var result = await _controller.GetFhirConfiguration(facilityId, CancellationToken.None);
            Assert.IsType<OkObjectResult>(result.Result);
        }

        [Fact]
        public async void GetFhirConfigurationNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IFhirQueryConfigurationManager>().Setup(x => x.GetAsync(It.IsAny<string>(), CancellationToken.None))
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
            _mocker.GetMock<IFhirQueryConfigurationManager>().Setup(x => x.AddAsync(It.IsAny<FhirQueryConfiguration>(), CancellationToken.None))
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
            _mocker.GetMock<IFhirQueryConfigurationManager>().Setup(x => x.GetAsync(It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync(new FhirQueryConfiguration());

            _mocker.GetMock<IFhirQueryConfigurationManager>().Setup(x => x.UpdateAsync(It.IsAny<FhirQueryConfiguration>(), CancellationToken.None))
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
            _mocker.GetMock<IFhirQueryConfigurationManager>()
                .Setup(x => x.DeleteAsync(It.IsAny<string>(), CancellationToken.None));

            var _controller = _mocker.CreateInstance<QueryConfigController>();

            var result = await _controller.DeleteFhirConfiguration(facilityId, CancellationToken.None);
            Assert.IsType<AcceptedResult>(result);
        }

        [Fact]
        public async void DeleteFhirConfigurationNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IFhirQueryConfigurationManager>()
                .Setup(x => x.DeleteAsync(It.IsAny<string>(), CancellationToken.None))
                .Throws(new NotFoundException());

            var _controller = _mocker.CreateInstance<QueryConfigController>();


            var result = await _controller.DeleteFhirConfiguration("NOT VALID", CancellationToken.None);
            var x = (ObjectResult)result;
            Assert.True(x.StatusCode == (int)HttpStatusCode.NotFound);
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
