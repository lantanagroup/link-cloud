using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.TenantCheck;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using MediatR;
using Moq;
using Moq.AutoMock;

namespace DataAcquisitionUnitTests.Commands.Config.Auth
{
    public class SaveAuthConfigCommandTests
    {
        private AutoMocker _mocker;
        private const string facilityId = "testId";

        [Fact]
        public async Task HandleTest()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<UpdateAuthConfigCommandHandler>();
            var command = new SaveAuthConfigCommand
            {
                QueryConfigurationTypePathParameter = QueryConfigurationTypePathParameter.fhirQueryConfiguration,
                FacilityId = facilityId,
                Configuration = new AuthenticationConfiguration()
            };

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Setup(r => r.SaveAuthenticationConfiguration(It.IsAny<string>(), It.IsAny<AuthenticationConfiguration>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Unit.Value));

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new FhirQueryConfiguration()));

            _mocker.GetMock<IMediator>()
                .Setup(m => m.Send(It.IsAny<CheckIfTenantExistsQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            var result = await handler.Handle(command, CancellationToken.None);

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Verify(r => r.SaveAuthenticationConfiguration(command.FacilityId, command.Configuration, It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.Equal(Unit.Value, result);
        }

        [Fact]
        public async Task HandleNegativeTest_NoFacilityConfig()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<UpdateAuthConfigCommandHandler>();
            var command = new SaveAuthConfigCommand
            {
                QueryConfigurationTypePathParameter = QueryConfigurationTypePathParameter.fhirQueryConfiguration,
                FacilityId = facilityId,
                Configuration = new AuthenticationConfiguration()
            };

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((FhirQueryConfiguration)null));

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Verify(r => r.SaveAuthenticationConfiguration(command.FacilityId, command.Configuration, It.IsAny<CancellationToken>()),
                Times.Never);

            await Assert.ThrowsAsync<MissingFacilityConfigurationException>(() => handler.Handle(command, CancellationToken.None));
        }
    }
}
