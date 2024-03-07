using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using Moq;
using Moq.AutoMock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            var result = await handler.Handle(query, CancellationToken.None);

            Assert.Null(result);
        }
    }
}
