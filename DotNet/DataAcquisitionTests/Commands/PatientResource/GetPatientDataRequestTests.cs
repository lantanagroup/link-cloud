using LantanaGroup.Link.DataAcquisition.Application.Commands.Census;
using LantanaGroup.Link.DataAcquisition.Application.Commands.PatientResource;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using Moq;
using Moq.AutoMock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAcquisitionUnitTests.Commands.PatientResource
{
    public class GetPatientDataRequestTests
    {
        private AutoMocker _mocker;
        private const string facilityId = "testId";

        [Fact]
        public async Task HandleTest()
        {
            _mocker = new AutoMocker { };
            var handler = _mocker.CreateInstance<GetPatientDataRequestHandler>();
            var request = new GetPatientDataRequest
            {
                FacilityId = facilityId,
                Message = new DataAcquisitionRequestedMessage
                {
                    PatientId = "testPatient",
                    ScheduledReports = new List<ScheduledReport>
                    {
                        new ScheduledReport
                        {
                            ReportType = "testReport"
                        }
                    },
                    QueryType = "Initial"
                },
                QueryPlanType = LantanaGroup.Link.DataAcquisition.Application.Models.QueryPlanType.InitialQueries,
                CorrelationId = "testCorrelation"
            };

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FhirQueryConfiguration());

            _mocker.GetMock<IQueryPlanRepository>()
                .Setup(r => r.GetQueryPlansByFacilityId(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<QueryPlan>());

            _mocker.GetMock<IFhirApiRepository>()
                .Setup(r => r.GetPatient(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AuthenticationConfiguration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Hl7.Fhir.Model.Patient());

            var result = await handler.Handle(request, CancellationToken.None);

            Assert.NotNull(result);
        }

        [Fact]
        public async Task HandleNegativeTest_No_FacilityId()
        {
            _mocker = new AutoMocker { };
            var handler = _mocker.CreateInstance<GetPatientDataRequestHandler>();
            var request = new GetPatientDataRequest
            {
                Message = new DataAcquisitionRequestedMessage
                {
                    PatientId = "testPatient",
                    ScheduledReports = new List<ScheduledReport>
                    {
                        new ScheduledReport
                        {
                            ReportType = "testReport"
                        }
                    }
                },
                CorrelationId = "testCorrelation"
            };

            _mocker.GetMock<IFhirQueryConfigurationRepository>()
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FhirQueryConfiguration());

            _mocker.GetMock<IQueryPlanRepository>()
                .Setup(r => r.GetQueryPlansByFacilityId(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<QueryPlan>());

            _mocker.GetMock<IFhirApiRepository>()
                .Setup(r => r.GetPatient(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AuthenticationConfiguration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Hl7.Fhir.Model.Patient());

            var result = await handler.Handle(request, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task HandleNegativeTest_No_PatientId()
        {
            _mocker = new AutoMocker { };
            var handler = _mocker.CreateInstance<GetPatientDataRequestHandler>();
            var request = new GetPatientDataRequest 
            { 
                FacilityId = facilityId, 
                Message = new DataAcquisitionRequestedMessage
                {
                    ScheduledReports = new List<ScheduledReport>
                    {
                        new ScheduledReport
                        {
                            ReportType = "testReport"
                        }
                    }
                },
                CorrelationId = "testCorrelation"
            };

            var result = await handler.Handle(request, CancellationToken.None);

            Assert.Null(result);
        }
    }
}
