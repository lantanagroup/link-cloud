using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using System.Net;
using Task = System.Threading.Tasks.Task;

namespace DataAcquisitionUnitTests.Controllers
{
    public class AuthenticationConfigControllerTests
    {
        private AutoMocker? _mocker;
        private const string facilityId = "testFacilityId";

        [Fact]
        public async void GetAuthenticationSettingsTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<GetAuthConfigQuery>(), CancellationToken.None))
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
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<GetAuthConfigQuery>(), CancellationToken.None))
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
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<CreateAuthConfigCommand>(), CancellationToken.None))
                .ReturnsAsync(new AuthenticationConfiguration());

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.CreateAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), new AuthenticationConfiguration(), CancellationToken.None);

            Assert.IsType<ActionResult<AuthenticationConfiguration>>(result);
            Assert.NotNull(((CreatedAtActionResult)result.Result).Value);
        }

        [Fact]
        public async Task CreateAuthenticationSettingsNegativeTest_InvalidFacilityId()
        {
            _mocker = new AutoMocker();

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.CreateAuthenticationSettings("", It.IsAny<QueryConfigurationTypePathParameter>(), new AuthenticationConfiguration(), CancellationToken.None);

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
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<UpdateAuthConfigCommand>(), CancellationToken.None))
                .Throws(new MissingFacilityConfigurationException());

            var controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await controller.CreateAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), new AuthenticationConfiguration(), CancellationToken.None);

            Assert.True(result.Value == null);
            Assert.IsType<ObjectResult>(result.Result);
            var objectResult = (ObjectResult)result.Result;
            Assert.True(((ProblemDetails)objectResult.Value).Status == (int)HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task UpdateAuthenticationSettingsTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<GetAuthConfigQuery>(), CancellationToken.None))
                .ReturnsAsync(new AuthenticationConfiguration());
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<UpdateAuthConfigCommand>(), CancellationToken.None))
                .ReturnsAsync(new AuthenticationConfiguration());

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.UpdateAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), new AuthenticationConfiguration(), CancellationToken.None);

            Assert.IsType<AcceptedResult>(result);
        }

        [Fact]
        public async Task UpdateAuthenticationSettingsNegativeTest_InvalidFacilityId()
        {
            _mocker = new AutoMocker();

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.UpdateAuthenticationSettings("", It.IsAny<QueryConfigurationTypePathParameter>(), new AuthenticationConfiguration(), CancellationToken.None);

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
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<GetAuthConfigQuery>(), CancellationToken.None))
                .ReturnsAsync(new AuthenticationConfiguration());
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<UpdateAuthConfigCommand>(), CancellationToken.None))
                .Throws(new Exception());

            _mocker.GetMock<IMediator>()
                .Setup(r => r.Send(It.IsAny<TriggerAuditEventCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Unit.Value);

            _mocker.GetMock<IMediator>()
                .Setup(m => m.Send(It.IsAny<CheckIfTenantExistsQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();


            try
            {
                var result = await _controller.UpdateAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), new AuthenticationConfiguration(), CancellationToken.None);
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
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<DeleteAuthConfigCommand>(), CancellationToken.None))
                .ReturnsAsync(new Unit());

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
