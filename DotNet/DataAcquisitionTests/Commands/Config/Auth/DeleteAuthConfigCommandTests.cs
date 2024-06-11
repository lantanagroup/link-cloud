using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using MediatR;
using Moq;
using Moq.AutoMock;

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

            try
            {
                var result = await handler.Handle(command, CancellationToken.None);
                Assert.Fail();
            }
            catch (BadRequestException ex)
            {
                _mocker.GetMock<IFhirQueryConfigurationRepository>()
                    .Verify(r => r.DeleteAuthenticationConfiguration(command.FacilityId, It.IsAny<CancellationToken>()),
                        Times.Never);

                Assert.True(true);
            }
        }

    }
}
