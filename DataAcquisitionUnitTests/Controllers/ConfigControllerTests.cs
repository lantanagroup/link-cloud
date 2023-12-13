using LantanaGroup.Link.DataAcquisition.Application.Commands.Config;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Entities;
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
    public class ConfigControllerTests
    {
        private AutoMocker? _mocker;
        private const string facilityId = "testFacilityId";

        [Fact]
        public async void GetConfigTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<GetConfigQuery>(), CancellationToken.None))
                .ReturnsAsync(new TenantDataAcquisitionConfigModel());

            var _controller = _mocker.CreateInstance<ConfigController>();

            var result = await _controller.GetConfig(facilityId, CancellationToken.None);
            Assert.IsType<OkObjectResult>(result.Result);
        }

        [Fact]
        public async void GetConfigNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<GetConfigQuery>(), CancellationToken.None))
                .ReturnsAsync((TenantDataAcquisitionConfigModel)null);

            var _controller = _mocker.CreateInstance<ConfigController>();

            var result = await _controller.GetConfig(facilityId, CancellationToken.None);
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async void CreateConfigTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<SaveConfigCommand>(), CancellationToken.None))
                .ReturnsAsync(Unit.Value);

            var _controller = _mocker.CreateInstance<ConfigController>();

            var result = await _controller.CreateConfig(new TenantDataAcquisitionConfigModel(), CancellationToken.None);
            Assert.IsType<AcceptedResult>(result);
        }

        [Fact]
        public async void CreateConfigNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<SaveConfigCommand>(), CancellationToken.None))
                .ReturnsAsync(null);

            var _controller = _mocker.CreateInstance<ConfigController>();

            var result = await _controller.CreateConfig(new TenantDataAcquisitionConfigModel(), CancellationToken.None);
            Assert.IsType<StatusCodeResult>(result);
        }

        [Fact]
        public async void UpdateConfigTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<SaveConfigCommand>(), CancellationToken.None))
                .ReturnsAsync(Unit.Value);

            var _controller = _mocker.CreateInstance<ConfigController>();

            var result = await _controller.UpdateConfig(facilityId, new TenantDataAcquisitionConfigModel(), CancellationToken.None);
            Assert.IsType<AcceptedResult>(result);
        }

        [Fact]
        public async void UpdateConfigNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<SaveConfigCommand>(), CancellationToken.None))
                .ReturnsAsync(null);

            var _controller = _mocker.CreateInstance<ConfigController>();

            var result = await _controller.UpdateConfig(facilityId, new TenantDataAcquisitionConfigModel(), CancellationToken.None);
            Assert.IsType<StatusCodeResult>(result);
        }

        [Fact]
        public async void DeleteConfigTest()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<DeleteConfigCommand>(), CancellationToken.None))
                .ReturnsAsync(Unit.Value);

            var _controller = _mocker.CreateInstance<ConfigController>();

            var result = await _controller.DeleteConfig(facilityId, CancellationToken.None);
            Assert.IsType<AcceptedResult>(result);
        }

        [Fact]
        public async void DeleteConfigNegativeTest_NullResult()
        {
            _mocker = new AutoMocker();
            _mocker.GetMock<IMediator>().Setup(x => x.Send(It.IsAny<DeleteConfigCommand>(), CancellationToken.None))
                .ReturnsAsync(null);

            var _controller = _mocker.CreateInstance<ConfigController>();

            var result = await _controller.DeleteConfig(facilityId, CancellationToken.None);
            Assert.IsType<StatusCodeResult>(result);
        }
    }
}
