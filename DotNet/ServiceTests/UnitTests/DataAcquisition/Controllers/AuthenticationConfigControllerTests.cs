using LantanaGroup.Link.DataAcquisition.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using System.Net;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using DataAcquisition.Domain.Application.Models;

namespace UnitTests.DataAcquisition.Controllers
{
    [Trait("Category", "UnitTests")]
    public class AuthenticationConfigControllerTests
    {
        private AutoMocker? _mocker;
        private const string facilityId = "testFacilityId";

        [Fact]
        public async void GetAuthenticationSettingsTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IFhirQueryConfigurationManager>().Setup(x => x.GetAuthenticationConfigurationByFacilityId(It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync(new AuthenticationConfiguration());

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.GetAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), CancellationToken.None);
            Assert.IsType<OkObjectResult>(result.Result);
        }

        [Fact]
        public async void GetAuthenticationSettingsNegativeTest_InvalidFacilityId()
        {
            _mocker = new AutoMocker();

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.GetAuthenticationSettings("", It.IsAny<QueryConfigurationTypePathParameter>(), CancellationToken.None);

            Assert.True(result.Value == null);
            Assert.IsType<ObjectResult>(result.Result);
            var objectResult = (ObjectResult)result.Result;
            Assert.True(((ProblemDetails)objectResult.Value).Status == (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetAuthenticationSettingsNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IFhirQueryConfigurationManager>().Setup(x => x.GetAuthenticationConfigurationByFacilityId(It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync((AuthenticationConfiguration)null);

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.GetAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), CancellationToken.None);

            Assert.True(result.Value == null);
            Assert.IsType<ObjectResult>(result.Result);
            var objectResult = (ObjectResult)result.Result;
            Assert.True(((ProblemDetails)objectResult.Value).Status == (int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CreateAuthenticationSettingsTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IFhirQueryConfigurationManager>().Setup(x => x.CreateAuthenticationConfiguration(It.IsAny<string>(), It.IsAny<AuthenticationConfiguration>(), CancellationToken.None))
                .ReturnsAsync(new AuthenticationConfiguration());

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.CreateAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), new AuthenticationConfigurationModel(), CancellationToken.None);

            Assert.IsType<ActionResult<AuthenticationConfiguration>>(result);
            Assert.NotNull(((CreatedAtActionResult)result.Result).Value);
        }

        [Fact]
        public async Task CreateAuthenticationSettingsNegativeTest_InvalidFacilityId()
        {
            _mocker = new AutoMocker();

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.CreateAuthenticationSettings("", It.IsAny<QueryConfigurationTypePathParameter>(), new AuthenticationConfigurationModel(), CancellationToken.None);

            Assert.True(result.Value == null);
            Assert.IsType<ObjectResult>(result.Result);
            var objectResult = (ObjectResult)result.Result;
            Assert.True(((ProblemDetails)objectResult.Value).Status == (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task CreateAuthenticationSettingsNegativeTest_NullBody()
        {
            _mocker = new AutoMocker();

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.CreateAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), null, CancellationToken.None);

            Assert.True(result.Value == null);
            Assert.IsType<ObjectResult>(result.Result);
            var objectResult = (ObjectResult)result.Result;
            Assert.True(((ProblemDetails)objectResult.Value).Status == (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task CreateAuthenticationSettingsNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IFhirQueryConfigurationManager>().Setup(x => x.CreateAuthenticationConfiguration(It.IsAny<string>(), It.IsAny<AuthenticationConfiguration>(), CancellationToken.None))
                .Throws(new NotFoundException());

            var controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await controller.CreateAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), new AuthenticationConfigurationModel(), CancellationToken.None);

            Assert.True(result.Value == null);
            Assert.IsType<ObjectResult>(result.Result);
            var objectResult = (ObjectResult)result.Result;
            Assert.True(((ProblemDetails)objectResult.Value).Status == (int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task UpdateAuthenticationSettingsTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IFhirQueryConfigurationManager>().Setup(x => x.GetAuthenticationConfigurationByFacilityId(It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync(new AuthenticationConfiguration());

            _mocker.GetMock<IFhirQueryConfigurationManager>().Setup(x => x.UpdateAuthenticationConfiguration(It.IsAny<string>(),It.IsAny<AuthenticationConfiguration>(), CancellationToken.None))
                .ReturnsAsync(new AuthenticationConfiguration());

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.UpdateAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), new AuthenticationConfigurationModel(), CancellationToken.None);

            Assert.IsType<AcceptedResult>(result);
        }

        [Fact]
        public async Task UpdateAuthenticationSettingsNegativeTest_InvalidFacilityId()
        {
            _mocker = new AutoMocker();

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.UpdateAuthenticationSettings("", It.IsAny<QueryConfigurationTypePathParameter>(), new AuthenticationConfigurationModel(), CancellationToken.None);

            Assert.IsType<ObjectResult>(result);
            var objectResult = (ObjectResult)result;
            Assert.NotNull(objectResult.Value);
            Assert.True(((ProblemDetails)objectResult.Value).Status == (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task UpdateAuthenticationSettingsNegativeTest_NullBody()
        {
            _mocker = new AutoMocker();

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.UpdateAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), null, CancellationToken.None);

            Assert.IsType<ObjectResult>(result);
            var objectResult = (ObjectResult)result;
            Assert.NotNull(objectResult.Value);
            Assert.True(((ProblemDetails)objectResult.Value).Status == (int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task UpdateAuthenticationSettingsNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IFhirQueryConfigurationManager>().Setup(x => x.GetAuthenticationConfigurationByFacilityId(It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync(new AuthenticationConfiguration());

            _mocker.GetMock<IFhirQueryConfigurationManager>().Setup(x => x.UpdateAuthenticationConfiguration(It.IsAny<string>(), It.IsAny<AuthenticationConfiguration>(), CancellationToken.None))
                .Throws(new Exception());

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            try
            {
                var result = await _controller.UpdateAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), new AuthenticationConfigurationModel(), CancellationToken.None);
                Assert.True(false);
            }
            catch (Exception)
            {
                Assert.True(true);
            }
        }

        [Fact]
        public async Task DeleteAuthenticationSettingsTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IFhirQueryConfigurationManager>().Setup(x =>
                x.DeleteAuthenticationConfiguration(It.IsAny<string>(), CancellationToken.None));

            _mocker.GetMock<IFhirQueryListConfigurationManager>().Setup(x =>
                x.DeleteAuthenticationConfiguration(It.IsAny<string>(), CancellationToken.None));

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.DeleteAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), CancellationToken.None);

            Assert.IsType<AcceptedResult>(result);
        }

        [Fact]
        public async Task DeleteAuthenticationSettingsNegativeTest_InvalidFacilityId()
        {
            _mocker = new AutoMocker();

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.DeleteAuthenticationSettings("", It.IsAny<QueryConfigurationTypePathParameter>(), CancellationToken.None);

            Assert.IsType<ObjectResult>(result);
            var objectResult = (ObjectResult)result;
            Assert.NotNull(objectResult.Value);
            Assert.True(((ProblemDetails)objectResult.Value).Status == (int)HttpStatusCode.BadRequest);
        }
    }
}
