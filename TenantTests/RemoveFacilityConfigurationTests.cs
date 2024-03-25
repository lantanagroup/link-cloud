
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Repository;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;

namespace TenantTests
{
    public class RemoveFacilityConfigurationTests
    {
        private FacilityConfigModel? _model;
        private MeasureApiConfig? _measureApiConfig;
        private AutoMocker? _mocker;
        private FacilityConfigurationService? _service;

        private const string facilityId = "TestFacility_002";
        private const string facilityName = "TestFacility_002";
        private const string id = "64b6dd4efe21820a49c1698a";

        public ILogger<FacilityConfigurationService> logger = Mock.Of<ILogger<FacilityConfigurationService>>();

        [Fact]
        public void TestRemoveFacility()
        {
            _mocker = new AutoMocker();

            _service = _mocker.CreateInstance<FacilityConfigurationService>();

            _model = new FacilityConfigModel()
            {
                Id = id,
                FacilityId = facilityId,
                FacilityName = facilityName,
                ScheduledTasks = new List<ScheduledTaskModel>(),
                MRPCreatedDate = DateTime.Now,
                MRPModifyDate = DateTime.Now
            };

            _measureApiConfig = new MeasureApiConfig()
            {
                MeasureServiceApiUrl = "test"
            };

            _mocker.GetMock<IFacilityConfigurationRepo>()
              .Setup(p => p.GetAsyncByFacilityId(facilityId, CancellationToken.None)).Returns(Task.FromResult<FacilityConfigModel>(_model));

            _mocker.GetMock<IFacilityConfigurationRepo>()
                .Setup(p => p.RemoveAsync(id, CancellationToken.None)).Returns(Task.FromResult<bool>(true));

            _mocker.GetMock<IKafkaProducerFactory<string, object>>()
            .Setup(p => p.CreateAuditEventProducer())
            .Returns(Mock.Of<IProducer<string, AuditEventMessage>>());

            _mocker.GetMock<IOptions<MeasureApiConfig>>()
            .Setup(p => p.Value)
            .Returns(_measureApiConfig);

            Task<string> facility = _service.RemoveFacility(facilityId, CancellationToken.None);

            _mocker.GetMock<IFacilityConfigurationRepo>().Verify(p => p.RemoveAsync(id, CancellationToken.None), Times.Once);

            Assert.NotEmpty(facility.Result);

        }

    }

}