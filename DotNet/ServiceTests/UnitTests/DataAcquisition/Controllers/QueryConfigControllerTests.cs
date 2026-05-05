using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Models;
using LantanaGroup.Link.Shared.Application.Services;
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
        public async System.Threading.Tasks.Task GetFhirConfigurationTest()
        {
            // Arrange
            var facilityId = "test-facility";
            var mocker = new AutoMocker();


            var min = DateTime.UtcNow.TimeOfDay;
            var max = DateTime.UtcNow.AddHours(5).TimeOfDay;

            var expectedConfig = new FhirQueryConfigurationModel
            {
                MinAcquisitionPullTime = min,
                MaxAcquisitionPullTime = max
            };

            mocker.GetMock<IFhirQueryConfigurationQueries>().Setup(x => x.GetByFacilityIdAsync(It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync(expectedConfig);

            mocker.GetMock<ITenantApiService>()
                .Setup(x => x.GetFacilityConfig(facilityId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LantanaGroup.Link.Shared.Application.Models.Tenant.FacilityModel { TimeZone = "America/Chicago" });

            var controller = mocker.CreateInstance<QueryConfigController>();

            // Act
            var result = await controller.GetFhirConfiguration(facilityId, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedConfig = Assert.IsType<ApiResultFhirQueryConfigurationModel>(okResult.Value);
            Assert.Equal(expectedConfig.MinAcquisitionPullTime, returnedConfig.MinAcquisitionPullTime);
            Assert.Equal(expectedConfig.MaxAcquisitionPullTime, returnedConfig.MaxAcquisitionPullTime);


            mocker.GetMock<IFhirQueryConfigurationQueries>()
                .Verify(x => x.GetByFacilityIdAsync(facilityId, It.IsAny<CancellationToken>()), Times.Once);

            mocker.GetMock<ITenantApiService>()
                .Verify(x => x.GetFacilityConfig(facilityId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async System.Threading.Tasks.Task GetFhirConfigurationNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IFhirQueryConfigurationQueries>().Setup(x => x.GetByFacilityIdAsync(It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync((FhirQueryConfigurationModel?)null);

            var _controller = _mocker.CreateInstance<QueryConfigController>();

            var result = await _controller.GetFhirConfiguration(facilityId, CancellationToken.None);

            Assert.True(result.Value == null);
            Assert.IsType<ObjectResult>(result.Result);
            var objectResult = (ObjectResult)result.Result;
            Assert.True(((ProblemDetails)objectResult.Value).Status == (int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async System.Threading.Tasks.Task GetFhirConfigurationNegativeTest_InvalidFacilityId()
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
        public async System.Threading.Tasks.Task CreateFhirConfigurationTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IFhirQueryConfigurationManager>().Setup(x => x.CreateAsync(It.IsAny<CreateFhirQueryConfigurationModel>(), CancellationToken.None))
                .ReturnsAsync(new FhirQueryConfigurationModel());

            var _controller = _mocker.CreateInstance<QueryConfigController>();

            var result = _controller.CreateFhirConfiguration(new ApiCreateFhirQueryConfigurationModel(), CancellationToken.None).Result;

            Assert.IsType<ActionResult<ApiResultFhirQueryConfigurationModel>>(result);
            Assert.NotNull(((CreatedAtActionResult)result.Result).Value);
        }

        [Fact]
        public async System.Threading.Tasks.Task CreateFhirConfigurationNegativeTest_NullBody()
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
        public async System.Threading.Tasks.Task UpdateFhirConfigurationTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IFhirQueryConfigurationQueries>().Setup(x => x.GetByFacilityIdAsync(It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync(new FhirQueryConfigurationModel());

            _mocker.GetMock<IFhirQueryConfigurationManager>().Setup(x => x.UpdateAsync(It.IsAny<UpdateFhirQueryConfigurationModel>(), CancellationToken.None))
                .ReturnsAsync(new FhirQueryConfigurationModel());

            var _controller = _mocker.CreateInstance<QueryConfigController>();

            var result = await _controller.UpdateFhirConfiguration(new ApiUpdateFhirQueryConfigurationModel() { FacilityId = "test", FhirServerBaseUrl = "test" }, CancellationToken.None);
            Assert.IsType<AcceptedResult>(result);
        }

        [Fact]
        public async System.Threading.Tasks.Task UpdateFhirConfigurationNegativeTest_NullBody()
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
        public async System.Threading.Tasks.Task DeleteFhirConfigurationTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IFhirQueryConfigurationManager>()
                .Setup(x => x.DeleteAsync(It.IsAny<string>(), CancellationToken.None));

            var _controller = _mocker.CreateInstance<QueryConfigController>();

            var result = await _controller.DeleteFhirConfiguration(facilityId, CancellationToken.None);
            Assert.IsType<AcceptedResult>(result);
        }

        [Fact]
        public async System.Threading.Tasks.Task DeleteFhirConfigurationNegativeTest_NullResult()
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
        public async System.Threading.Tasks.Task DeleteFhirConfigurationNegativeTest_InvalidFacilityId()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryConfigController>();

            var result = await _controller.DeleteFhirConfiguration("", CancellationToken.None);

            Assert.IsType<ObjectResult>(result);
            var objectResult = (ObjectResult)result;
            Assert.NotNull(objectResult.Value);
            Assert.True(((ProblemDetails)objectResult.Value).Status == (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async System.Threading.Tasks.Task CreateFhirConfigurationNegativeTest_AlreadyExists_ReturnsConflict()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IFhirQueryConfigurationManager>()
                .Setup(x => x.CreateAsync(It.IsAny<CreateFhirQueryConfigurationModel>(), CancellationToken.None))
                .ThrowsAsync(new EntityAlreadyExistsException("A FhirQueryConfiguration already exists for facilityId."));

            var _controller = _mocker.CreateInstance<QueryConfigController>();

            var result = await _controller.CreateFhirConfiguration(
                new ApiCreateFhirQueryConfigurationModel { FacilityId = facilityId, FhirServerBaseUrl = "http://example.com" },
                CancellationToken.None);

            Assert.IsType<ObjectResult>(result.Result);
            var objectResult = (ObjectResult)result.Result;
            Assert.Equal((int)HttpStatusCode.Conflict, objectResult.StatusCode);
        }

        [Fact]
        public async System.Threading.Tasks.Task UpdateFhirConfigurationNegativeTest_NotFound()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IFhirQueryConfigurationManager>()
                .Setup(x => x.UpdateAsync(It.IsAny<UpdateFhirQueryConfigurationModel>(), CancellationToken.None))
                .ThrowsAsync(new NotFoundException("No configuration found for facilityId."));

            var _controller = _mocker.CreateInstance<QueryConfigController>();

            var result = await _controller.UpdateFhirConfiguration(
                new ApiUpdateFhirQueryConfigurationModel { FacilityId = "NonExisting", FhirServerBaseUrl = "http://example.com" },
                CancellationToken.None);

            Assert.IsType<ObjectResult>(result);
            var objectResult = (ObjectResult)result;
            Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
        }

        [Fact]
        public async System.Threading.Tasks.Task UpdateFhirConfigurationNegativeTest_InvalidFacilityId()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IFhirQueryConfigurationManager>()
                .Setup(x => x.UpdateAsync(It.IsAny<UpdateFhirQueryConfigurationModel>(), CancellationToken.None))
                .ThrowsAsync(new ArgumentNullException("FacilityId"));

            var _controller = _mocker.CreateInstance<QueryConfigController>();

            var result = await _controller.UpdateFhirConfiguration(
                new ApiUpdateFhirQueryConfigurationModel(),
                CancellationToken.None);

            Assert.IsType<ObjectResult>(result);
            var objectResult = (ObjectResult)result;
            Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        }
    }
}
