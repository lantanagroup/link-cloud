
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Tenant
{
    [Trait("Category", "UnitTests")]
    public class RemoveFacilityConfigurationTests
    {
        private Facility? _model;
        private ServiceRegistry? _serviceRegistry;
        private AutoMocker? _mocker;
        private FacilityConfigurationService? _service;

        private const string facilityId = "TestFacility_002";
        private const string facilityName = "TestFacility_002";
        private Guid id = Guid.Parse("7241D6DA-4D15-4A41-AECC-08DC4DB45323");

        public ILogger<FacilityConfigurationService> logger = Mock.Of<ILogger<FacilityConfigurationService>>();

        [Fact]
        public void TestRemoveFacility()
        {
            _mocker = new AutoMocker();

            _service = _mocker.CreateInstance<FacilityConfigurationService>();

            _model = new Facility()
            {
                Id = id,
                FacilityId = facilityId,
                FacilityName = facilityName,
                ScheduledReports = new ScheduledReportModel()
            };
            _model.ScheduledReports.Daily = new string[] { "NHSNdQMAcuteCareHospitalInitialPopulation" };
            _model.ScheduledReports.Weekly = new string[] { "NHSNRespiratoryPathogenSurveillanceInitialPopulation" };
            _model.ScheduledReports.Monthly = new string[] { "NHSNGlycemicControlHypoglycemicInitialPopulation" };

            _serviceRegistry = new ServiceRegistry()
            {
                MeasureServiceUrl = "test"
            };

            _mocker.GetMock<IEntityRepository<Facility>>()
              .Setup(p => p.FirstOrDefaultAsync((x => x.FacilityId == facilityId), CancellationToken.None)).Returns(Task.FromResult(_model));

            _mocker.GetMock<IEntityRepository<Facility>>()
                .Setup(p => p.DeleteAsync(_model, CancellationToken.None)).Returns(Task.FromResult<bool>(true));

            _mocker.GetMock<IKafkaProducerFactory<string, object>>()
            .Setup(p => p.CreateAuditEventProducer(false))
            .Returns(Mock.Of<IProducer<string, AuditEventMessage>>());

            _mocker.GetMock<IOptions<MeasureConfig>>()
              .Setup(p => p.Value)
              .Returns(new MeasureConfig());

            _mocker.GetMock<IOptions<ServiceRegistry>>()
                .Setup(p => p.Value)
                .Returns(_serviceRegistry);

            Task<string> facility = _service.RemoveFacility(facilityId, CancellationToken.None);

            _mocker.GetMock<IEntityRepository<Facility>>().Verify(p => p.DeleteAsync(_model, CancellationToken.None), Times.Once);

            Assert.NotEmpty(facility.Result);

        }

    }

}