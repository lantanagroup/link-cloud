using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using MediatR;
using Moq;
using Moq.AutoMock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAcquisitionUnitTests.Commands.Config.QueryConfig
{
    public class DeleteFhirQueryConfigurationCommandTests
    {
        private AutoMocker? _mocker;
        private const string facilityId = "testFacilityId";

        [Fact]
        public async Task HandleTest()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<DeleteFhirQueryConfigurationCommandHandler>();
            var command = new DeleteFhirQueryConfigurationCommand
            {
                FacilityId = facilityId
            };

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Setup(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Unit.Value));

            var result = await handler.Handle(command, CancellationToken.None);

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Verify(r => r.DeleteAsync(command.FacilityId, It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.Equal(Unit.Value, result);
        }

        [Fact]
        public async Task HandleNegativeTest_NoFacilityId()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<DeleteFhirQueryConfigurationCommandHandler>();
            var command = new DeleteFhirQueryConfigurationCommand
            {
                FacilityId = "InvalidId"
            };

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Setup(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Invalid FacilityId"));

            await Assert.ThrowsAsync<Exception>(() => handler.Handle(command, CancellationToken.None));

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Verify(r => r.DeleteAsync(command.FacilityId, It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
