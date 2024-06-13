using Confluent.Kafka.Admin;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Census;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryList;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using Moq;
using Moq.AutoMock;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Hl7.Fhir.Model.List;

namespace DataAcquisitionUnitTests.Commands.Census
{
    public class GetPatientCensusRequestTests
    {
        private AutoMocker _mocker;
        private const string facilityId = "testId";

        [Fact]
        public async Task HandleTest()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<GetPatientCensusRequestHandler>();
            var request = new GetPatientCensusRequest { FacilityId = facilityId };
            var facilityConfig = new FhirListConfiguration { FhirBaseServerUrl = "testUrl",
                FacilityId = facilityId,
                EHRPatientLists = new List<EhrPatientList>
                {
                    new EhrPatientList
                    {
                        ListIds = new List<string> { "patient1", "patient2" }
                    }
                },
                Authentication = new AuthenticationConfiguration() };
            List<Hl7.Fhir.Model.ResourceReference> codeList = facilityConfig.EHRPatientLists[0].ListIds.ConvertAll(value => new Hl7.Fhir.Model.ResourceReference("testSystem", value));
            var fhirList = new Hl7.Fhir.Model.List()
            {
                Entry = codeList.ConvertAll(entry => new EntryComponent { Item = entry })
            };
            _mocker.GetMock<IFhirQueryListConfigurationRepository>()
                .Setup(r => r.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(facilityConfig);

            _mocker.GetMock<IFhirApiRepository>()
                .Setup(r => r.GetPatientList(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AuthenticationConfiguration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fhirList);

            var result = await handler.Handle(request, CancellationToken.None);

            _mocker.GetMock<IFhirQueryListConfigurationRepository>()
                .Verify(r => r.GetByFacilityIdAsync(request.FacilityId, It.IsAny<CancellationToken>()),
                Times.Once);
            _mocker.GetMock<IFhirApiRepository>()
                .Verify(r => r.GetPatientList(facilityConfig.FhirBaseServerUrl, It.IsAny<string>(), It.IsAny<AuthenticationConfiguration>(), It.IsAny<CancellationToken>()),
                Times.Exactly(facilityConfig.EHRPatientLists[0].ListIds.Count));

            Assert.NotNull(result);
            var patientIds = (result as PatientIDsAcquiredMessage);
            Assert.NotNull(patientIds?.PatientIds);
        }

        [Fact]
        public async Task HandleTest_NoFacilityConfig()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<GetPatientCensusRequestHandler>();
            var request = new GetPatientCensusRequest { FacilityId = facilityId };

            _mocker.GetMock<IFhirQueryListConfigurationRepository>()
                .Setup(r => r.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((FhirListConfiguration)null);

            await Assert.ThrowsAsync<Exception>(() => handler.Handle(request, CancellationToken.None));

            _mocker.GetMock<IFhirApiRepository>()
                .Verify(r => r.GetPatientList(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AuthenticationConfiguration>(),It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleTest_NoList()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<GetPatientCensusRequestHandler>();
            var request = new GetPatientCensusRequest { FacilityId = facilityId };
            var facilityConfig = new FhirListConfiguration { FacilityId = facilityId };
            var patientList = new List<string> { "patient1", "patient2" };

            _mocker.GetMock<IFhirQueryListConfigurationRepository>()
                .Setup(r => r.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(facilityConfig);

            _mocker.GetMock<IFhirApiRepository>()
                .Setup(r => r.GetPatientList(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AuthenticationConfiguration>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new NullReferenceException("Error Fetching patient list"));

            await Assert.ThrowsAsync<NullReferenceException>(() => handler.Handle(request, CancellationToken.None));

            _mocker.GetMock<IFhirQueryListConfigurationRepository>()
                .Verify(r => r.GetByFacilityIdAsync(request.FacilityId, It.IsAny<CancellationToken>()),
                Times.Once);

            _mocker.GetMock<IFhirApiRepository>()
                .Verify(r => r.GetPatientList(facilityConfig.FhirBaseServerUrl, It.IsAny<string>(), It.IsAny<AuthenticationConfiguration>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
