using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.TenantCheck;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using MediatR;
using Moq;
using Moq.AutoMock;

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

            var result = await handler.Handle(command, CancellationToken.None);

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Verify(r => r.UpdateAsync(command.queryConfiguration, It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.Equal(existingConfig.FacilityId, result.FacilityId);
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
                .ReturnsAsync(command.queryConfiguration);

            _mocker.GetMock<IMediator>()
                .Setup(m => m.Send(It.IsAny<CheckIfTenantExistsQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            var result = await handler.Handle(command, CancellationToken.None);

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Verify(r => r.AddAsync(command.queryConfiguration, It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.Equal(command.queryConfiguration, result);
            Assert.NotNull(command.queryConfiguration.ModifyDate);
        }
    }
}
