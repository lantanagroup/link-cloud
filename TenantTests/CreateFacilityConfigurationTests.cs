
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository;
using LantanaGroup.Link.Tenant.Services;
using LantanaGroup.Link.Tenant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using static LantanaGroup.Link.Tenant.Entities.ScheduledTaskModel;

namespace TenantTests
{
    public class CreateFacilityConfigurationTests
    {
        private FacilityConfigModel? _model;
        private TenantConfig? _tenantConfig;
        private const string facilityId = "TestFacility_002";
        private const string facilityName = "TestFacility_002";
        private static readonly List<ScheduledTaskModel> scheduledTaskModels = new List<ScheduledTaskModel>();

        private AutoMocker?_mocker;
        private FacilityConfigurationService? _service;

        public ILogger<FacilityConfigurationService> logger = Mock.Of<ILogger<FacilityConfigurationService>>();

        [Fact]
        public void TestCreateFacility()
        {
            scheduledTaskModels.Add(new ScheduledTaskModel() { KafkaTopic = KafkaTopic.ReportScheduled.ToString(), ReportTypeSchedules = new List<ReportTypeSchedule>() });

            _model = new FacilityConfigModel()
            {
                FacilityId = facilityId,
                FacilityName = facilityName,
                ScheduledTasks = new List<ScheduledTaskModel>(),
                MRPCreatedDate = DateTime.Now,
                MRPModifyDate = DateTime.Now
            };

            _tenantConfig = new TenantConfig()
            {
                MeasureDefUrl = "test" 
            };

            _model.ScheduledTasks.AddRange(scheduledTaskModels);

            _mocker = new AutoMocker();

           _service = _mocker.CreateInstance<FacilityConfigurationService>();

            _ = _mocker.GetMock<IFacilityConfigurationRepo>()
                .Setup(p => p.CreateAsync(_model, CancellationToken.None)).Returns(Task.FromResult<bool>(true));  

            _ = _mocker.GetMock<IKafkaProducerFactory<string, object>>()
            .Setup(p => p.CreateAuditEventProducer())
            .Returns(Mock.Of<IProducer<string, AuditEventMessage>>());

            _mocker.GetMock<IOptions<TenantConfig>>()
              .Setup(p => p.Value)
              .Returns(_tenantConfig);

            Task<string> _createdFacilityId = _service.CreateFacility(_model,CancellationToken.None);

            _mocker.GetMock<IFacilityConfigurationRepo>().Verify(p => p.CreateAsync(_model, CancellationToken.None), Times.Once);

             Assert.NotEmpty(_createdFacilityId.Result);
        }

        [Fact]
        public async Task TestErrorCreateDuplicateFacility()
        {
            List<ScheduledTaskModel> scheduledTaskModels = new List<ScheduledTaskModel>();

            scheduledTaskModels.Add(new ScheduledTaskModel() { KafkaTopic = KafkaTopic.ReportScheduled.ToString(), ReportTypeSchedules = new List<ReportTypeSchedule>() });

            List<FacilityConfigModel> facilities = new List<FacilityConfigModel>();

            _model = new FacilityConfigModel()
            {
                FacilityId = facilityId,
                FacilityName = facilityName,
                ScheduledTasks = new List<ScheduledTaskModel>(),
                MRPCreatedDate = DateTime.Now,
                MRPModifyDate = DateTime.Now
            };

            _tenantConfig = new TenantConfig()
            {
                MeasureDefUrl = "test"
            };


            facilities.Add(_model);

            _model.ScheduledTasks.AddRange(scheduledTaskModels);

            _mocker = new AutoMocker();

            _service = _mocker.CreateInstance<FacilityConfigurationService>();

            _ = _mocker.GetMock<IFacilityConfigurationRepo>()
                .Setup(p => p.CreateAsync(_model, CancellationToken.None)).Returns(Task.FromResult<bool>(true));

            _ = _mocker.GetMock<IKafkaProducerFactory<string, object>>()
                .Setup(p => p.CreateAuditEventProducer())
                .Returns(Mock.Of<IProducer<string, AuditEventMessage>>());

            _mocker.GetMock<IOptions<TenantConfig>>()
             .Setup(p => p.Value)
             .Returns(_tenantConfig);

            Task<string> _createdFacilityId = _service.CreateFacility(_model, CancellationToken.None);

            _ = await Assert.ThrowsAsync<ApplicationException>(() => _service.CreateFacility(_model, CancellationToken.None));

        }


    }
}