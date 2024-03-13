using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryList;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using MongoDB.Driver;
using Moq;
using Moq.AutoMock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAcquisitionUnitTests.Commands.Config.QueryList
{
    public class GetFhirListConfigQueryTests
    {
        private AutoMocker _mocker;
        private const string facilityId = "testId";

        [Fact]
        public async Task HandleTest()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<GetFhirListConfigQueryHandler>();
            var query = new GetFhirListConfigQuery { FacilityId = facilityId };
            var expectedResult = new FhirListConfiguration();

            _mocker.GetMock<IFhirQueryListConfigurationRepository>()
                .Setup(r => r.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var result = await handler.Handle(query, CancellationToken.None);

            _mocker.GetMock<IFhirQueryListConfigurationRepository>()
                .Verify(r => r.GetByFacilityIdAsync(query.FacilityId, It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.Same(expectedResult, result);
        }

        [Fact]
        public async Task HandleNegativeTest_Without_FacilityId()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<GetFhirListConfigQueryHandler>();
            var query = new GetFhirListConfigQuery { FacilityId = null };

            await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(query, CancellationToken.None));

            _mocker.GetMock<IFhirQueryListConfigurationRepository>()
                .Verify(r => r.GetByFacilityIdAsync(query.FacilityId, It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleNegativeTest_Without_Config()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<GetFhirListConfigQueryHandler>();
            var query = new GetFhirListConfigQuery { FacilityId = facilityId };

            _mocker.GetMock<IFhirQueryListConfigurationRepository>()
                .Setup(r => r.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((FhirListConfiguration)null);

            await Assert.ThrowsAsync<MissingTenantConfigurationException>(() => handler.Handle(query, CancellationToken.None));

            _mocker.GetMock<IFhirQueryListConfigurationRepository>()
                .Verify(r => r.GetByFacilityIdAsync(query.FacilityId, It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
