using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.TenantCheck;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Services;
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
    public class SaveFhirQueryConfigCommandTests
    {
        private AutoMocker _mocker;
        private const string facilityId = "testId";

        [Fact]
        public async Task HandleTest()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<SaveFhirQueryConfigCommandHandler>();
            var command = new SaveFhirQueryConfigCommand
            {
                queryConfiguration = new FhirQueryConfiguration { FacilityId = facilityId }
            };
            var existingConfig = new FhirQueryConfiguration { FacilityId = facilityId };

            _mocker.GetMock<IMediator>()
                .Setup(m => m.Send(It.IsAny<GetFhirQueryConfigQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingConfig);

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Setup(r => r.UpdateAsync(It.IsAny<FhirQueryConfiguration>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(existingConfig));

            _mocker.GetMock<IMediator>()
                .Setup(m => m.Send(It.IsAny<CheckIfTenantExistsQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            _mocker.GetMock<ITenantApiService>()
                .Setup(s => s.CheckFacilityExists(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            var result = await handler.Handle(command, CancellationToken.None);

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Verify(r => r.UpdateAsync(command.queryConfiguration, It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.Equal(Unit.Value, result);
        }

        [Fact]
        public async Task HandleTest_WithNullModifyDate()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<SaveFhirQueryConfigCommandHandler>();
            var command = new SaveFhirQueryConfigCommand
            {
                queryConfiguration = new FhirQueryConfiguration { FacilityId = facilityId }
            };

            _mocker.GetMock<IMediator>()
                .Setup(m => m.Send(It.IsAny<GetFhirQueryConfigQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((FhirQueryConfiguration)null));

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Setup(r => r.AddAsync(It.IsAny<FhirQueryConfiguration>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Unit.Value));

            _mocker.GetMock<IMediator>()
                .Setup(m => m.Send(It.IsAny<CheckIfTenantExistsQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            _mocker.GetMock<ITenantApiService>()
                .Setup(s => s.CheckFacilityExists(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            var result = await handler.Handle(command, CancellationToken.None);

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Verify(r => r.AddAsync(command.queryConfiguration, It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.Equal(Unit.Value, result);
            Assert.NotNull(command.queryConfiguration.ModifyDate);
        }
    }
}
