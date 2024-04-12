using Amazon.Runtime.Internal.Util;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAcquisitionUnitTests.Commands.Config.Auth
{
    public class DeleteAuthConfigCommandTests
    {
        private AutoMocker? _mocker;
        private const string facilityId = "testFacilityId";

        [Fact]
        public async Task HandleTest()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<DeleteAuthConfigCommandHandler>();
            var command = new DeleteAuthConfigCommand
            {
                QueryConfigurationTypePathParameter = QueryConfigurationTypePathParameter.fhirQueryConfiguration,
                FacilityId = facilityId
            };

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Setup(r => r.DeleteAuthenticationConfiguration(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Unit.Value));

            var result = await handler.Handle(command, CancellationToken.None);

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Verify(r => r.DeleteAuthenticationConfiguration(command.FacilityId, It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.Equal(Unit.Value, result);
        }

        [Fact]
        public async Task HandleNegativeTest_NoFacilityId()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<DeleteAuthConfigCommandHandler>();
            var command = new DeleteAuthConfigCommand
            {
                QueryConfigurationTypePathParameter = QueryConfigurationTypePathParameter.fhirQueryConfiguration,
                FacilityId = null
            };

            var result = await handler.Handle(command, CancellationToken.None);

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Verify(r => r.DeleteAuthenticationConfiguration(command.FacilityId, It.IsAny<CancellationToken>()),
                Times.Never);

            Assert.Equal(Unit.Value, result);
        }

    }
}
