using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryList;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.TenantCheck;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using MediatR;
using Moq;
using Moq.AutoMock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAcquisitionUnitTests.Commands.Config.QueryList
{
    public class SaveFhirListCommandTests
    {
        private AutoMocker _mocker;
        private const string facilityId = "testId";

        [Fact]
        public async Task HandleTest()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<SaveFhirListCommandHandler>();
            var command = new SaveFhirListCommand
            {
                FacilityId = facilityId,
                FhirListConfiguration = new FhirListConfiguration()
            };
            var existingConfig = new FhirListConfiguration { Id = Guid.NewGuid(), FacilityId = facilityId, CreateDate = DateTime.UtcNow };

            _mocker.GetMock<IFhirQueryListConfigurationRepository>()
                .Setup(r => r.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingConfig);

            _mocker.GetMock<IFhirQueryListConfigurationRepository>()
                .Setup(r => r.UpdateAsync(It.IsAny<FhirListConfiguration>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(existingConfig));

            _mocker.GetMock<IMediator>()
                .Setup(m => m.Send(It.IsAny<CheckIfTenantExistsQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            await handler.Handle(command, CancellationToken.None);

            _mocker.GetMock<IFhirQueryListConfigurationRepository>()
                .Verify(r => r.UpdateAsync(existingConfig, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task HandleTest_Without_Config()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<SaveFhirListCommandHandler>();
            var command = new SaveFhirListCommand
            {
                FacilityId = facilityId,
                FhirListConfiguration = new FhirListConfiguration()
            };
            var existingConfig = new FhirListConfiguration();

            _mocker.GetMock<IFhirQueryListConfigurationRepository>()
                .Setup(r => r.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((FhirListConfiguration)null);

            _mocker.GetMock<IFhirQueryListConfigurationRepository>()
                .Setup(r => r.AddAsync(It.IsAny<FhirListConfiguration>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(existingConfig));

            _mocker.GetMock<IMediator>()
                .Setup(m => m.Send(It.IsAny<CheckIfTenantExistsQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            await handler.Handle(command, CancellationToken.None);

            _mocker.GetMock<IFhirQueryListConfigurationRepository>()
                .Verify(r => r.AddAsync(command.FhirListConfiguration, It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
