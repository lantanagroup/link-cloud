using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using Moq;
using Moq.AutoMock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAcquisitionUnitTests.Commands.Config.Auth
{
    public class GetAuthConfigQueryTests
    {
        private AutoMocker _mocker;
        private const string facilityId = "testId";

        [Fact]
        public async Task HandleTest()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<GetAuthConfigQueryHandler>();
            var query = new GetAuthConfigQuery { QueryConfigurationTypePathParameter = QueryConfigurationTypePathParameter.fhirQueryConfiguration, FacilityId = facilityId };
            var expectedResult = new AuthenticationConfiguration();

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Setup(r => r.GetAuthenticationConfigurationByFacilityId(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var result = await handler.Handle(query, CancellationToken.None);

            Assert.Same(expectedResult, result);
        }

        [Fact]
        public async Task HandleNegativeTest_Without_QueryConfigurationTypePathParameter()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<GetAuthConfigQueryHandler>();
            var query = new GetAuthConfigQuery { QueryConfigurationTypePathParameter = null, FacilityId = facilityId };
            var expectedResult = new AuthenticationConfiguration();

            var result = await handler.Handle(query, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task HandleNegativeTest_Without_FacilityId()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<GetAuthConfigQueryHandler>();
            var query = new GetAuthConfigQuery { QueryConfigurationTypePathParameter = QueryConfigurationTypePathParameter.fhirQueryConfiguration, FacilityId = null };
            var expectedResult = new AuthenticationConfiguration();

            var result = await handler.Handle(query, CancellationToken.None);

            Assert.Null(result);
        }
    }
}
