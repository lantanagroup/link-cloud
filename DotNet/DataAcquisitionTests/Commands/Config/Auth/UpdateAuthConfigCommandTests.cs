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
    public class UpdateAuthConfigCommandTests
    {
        private AutoMocker _mocker;
        private const string facilityId = "testId";

        [Fact]
        public async Task Create_And_Update_AuthenticationConfiguration_Test()
        {
            _mocker = new AutoMocker();

            var createHandler = _mocker.CreateInstance<CreateAuthConfigCommandHandler>();
            var createCommand = new CreateAuthConfigCommand()
            {
                QueryConfigurationTypePathParameter = QueryConfigurationTypePathParameter.fhirQueryConfiguration,
                FacilityId = facilityId,
                Configuration = new AuthenticationConfiguration()
                {
                    Audience = "TestAudience_Original",
                    AuthType = "TestAuthType_Original",
                    ClientId = facilityId
                }
            };

            var handler = _mocker.CreateInstance<UpdateAuthConfigCommandHandler>();
            var command = new UpdateAuthConfigCommand
            {
                QueryConfigurationTypePathParameter = QueryConfigurationTypePathParameter.fhirQueryConfiguration,
                FacilityId = facilityId,
                Configuration = new AuthenticationConfiguration()
                {
                    Audience = "TestAudience_Updated",
                    AuthType = "TestAuthType_Updated",
                    ClientId = facilityId
                }
            };

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Setup(r => r.CreateAuthenticationConfiguration(It.IsAny<string>(), It.IsAny<AuthenticationConfiguration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(createCommand.Configuration);


            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Setup(r => r.UpdateAuthenticationConfiguration(It.IsAny<string>(), It.IsAny<AuthenticationConfiguration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(command.Configuration);

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Setup(r => r.GetAsync(It.IsAny<string>(), CancellationToken.None))
                .Returns(Task.FromResult(new FhirQueryConfiguration()));

            _mocker.GetMock<IMediator>()
                .Setup(m => m.Send(It.IsAny<CheckIfTenantExistsQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            var createResult = await createHandler.Handle(createCommand, CancellationToken.None);
            var result = await handler.Handle(command, CancellationToken.None);

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Verify(r => r.UpdateAuthenticationConfiguration(command.FacilityId, command.Configuration, It.IsAny<CancellationToken>()),
                Times.Once);


            Assert.NotNull(createResult);
            Assert.NotNull(createResult.Audience);
            Assert.NotNull(createResult.AuthType);

            Assert.NotNull(result);
            Assert.NotNull(result.Audience);
            Assert.NotNull(result.AuthType);

            Assert.NotEqual(createResult.AuthType, result.AuthType);
            Assert.NotEqual(createResult.Audience, result.Audience);
        }

        [Fact]
        public async Task HandleNegativeTest_NoFacilityConfig()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<UpdateAuthConfigCommandHandler>();
            var command = new UpdateAuthConfigCommand
            {
                QueryConfigurationTypePathParameter = QueryConfigurationTypePathParameter.fhirQueryConfiguration,
                FacilityId = facilityId,
                Configuration = new AuthenticationConfiguration()
            };

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Setup(r => r.GetAsync(It.IsAny<string>(), CancellationToken.None))
                .Returns(Task.FromResult((FhirQueryConfiguration)null));

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Verify(r => r.UpdateAuthenticationConfiguration(command.FacilityId, command.Configuration, It.IsAny<CancellationToken>()),
                Times.Never);

            await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
        }
    }
}
