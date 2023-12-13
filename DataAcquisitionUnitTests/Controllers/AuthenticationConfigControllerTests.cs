using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            Assert.IsType<BadRequestResult>(result.Result);
        }

        [Fact]
        public async void GetAuthenticationSettingsNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<GetAuthConfigQuery>(), CancellationToken.None))
                .ReturnsAsync((AuthenticationConfiguration)null);

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.GetAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), CancellationToken.None);
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task CreateAuthenticationSettingsTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<SaveAuthConfigCommand>(), CancellationToken.None))
                .ReturnsAsync(new Unit());

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.CreateAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), new AuthenticationConfiguration(), CancellationToken.None);

            Assert.IsType<AcceptedResult>(result);
        }

        [Fact]
        public async Task CreateAuthenticationSettingsNegativeTest_InvalidFacilityId()
        {
            _mocker = new AutoMocker();

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.CreateAuthenticationSettings("", It.IsAny<QueryConfigurationTypePathParameter>(), new AuthenticationConfiguration(), CancellationToken.None);

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task CreateAuthenticationSettingsNegativeTest_NullBody()
        {
            _mocker = new AutoMocker();

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.CreateAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), null, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreateAuthenticationSettingsNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<SaveAuthConfigCommand>(), CancellationToken.None))
                .ReturnsAsync(null);

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.CreateAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), new AuthenticationConfiguration(), CancellationToken.None);

            Assert.IsType<StatusCodeResult>(result);
        }

        [Fact]
        public async Task UpdateAuthenticationSettingsTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<GetAuthConfigQuery>(), CancellationToken.None))
                .ReturnsAsync(new AuthenticationConfiguration());
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<SaveAuthConfigCommand>(), CancellationToken.None))
                .ReturnsAsync(new Unit());

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

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task UpdateAuthenticationSettingsNegativeTest_NullBody()
        {
            _mocker = new AutoMocker();

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.UpdateAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), null, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UpdateAuthenticationSettingsNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<GetAuthConfigQuery>(), CancellationToken.None))
                .ReturnsAsync(new AuthenticationConfiguration());
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<SaveAuthConfigCommand>(), CancellationToken.None))
                .ReturnsAsync(null);

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.UpdateAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), new AuthenticationConfiguration(), CancellationToken.None);

            Assert.IsType<StatusCodeResult>(result);
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

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task DeleteAuthenticationSettingsNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<DeleteAuthConfigCommand>(), CancellationToken.None))
                .ReturnsAsync(null);

            var _controller = _mocker.CreateInstance<AuthenticationConfigController>();

            var result = await _controller.DeleteAuthenticationSettings(facilityId, It.IsAny<QueryConfigurationTypePathParameter>(), CancellationToken.None);

            Assert.IsType<StatusCodeResult>(result);
        }
    }
}
