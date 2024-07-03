using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using Moq;
using Moq.AutoMock;

namespace DataAcquisitionUnitTests.Commands.Config.QueryConfig
{
    public class GetFhirQueryConfigQueryTests
    {
        private AutoMocker _mocker;
        private const string facilityId = "testId";

        [Fact]
        public async Task HandleTest()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<GetFhirQueryConfigQueryHandler>();
            var query = new GetFhirQueryConfigQuery { FacilityId = facilityId };
            var expectedResult = new FhirQueryConfiguration();

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var result = await handler.Handle(query, CancellationToken.None);

            Assert.Same(expectedResult, result);
        }

        [Fact]
        public async Task HandleNegativeTest_NoFacilityId()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<GetFhirQueryConfigQueryHandler>();
            var query = new GetFhirQueryConfigQuery { FacilityId = null };

            try
            {
                var result = await handler.Handle(query, CancellationToken.None);
            }
            catch (BadRequestException e)
            {
                Assert.True(true);
                return;
            }

            Assert.Fail();
        }
    }
}
