
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Repository.Interfaces.Sql;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using static LantanaGroup.Link.Tenant.Entities.ScheduledTaskModel;
using static LantanaGroup.Link.Shared.Application.Extensions.Security.BackendAuthenticationServiceExtension;

namespace TenantTests
{
    public class CreateFacilityConfigurationTests
    {
        private FacilityConfigModel? _model;
        private ServiceRegistry? _serviceRegistry;
        private LinkBearerServiceOptions _linkBearerServiceOptions;
        private const string facilityId = "TestFacility_002";
        private const string facilityName = "TestFacility_002";
        private static readonly List<ScheduledTaskModel> scheduledTaskModels = new List<ScheduledTaskModel>();

        private AutoMocker?_mocker;
        private FacilityConfigurationService? _service;

        public ILogger<FacilityConfigurationService> logger = Mock.Of<ILogger<FacilityConfigurationService>>();

        [Fact]
        public async Task TestCreateFacility()
        {
            scheduledTaskModels.Add(new ScheduledTaskModel() { KafkaTopic = KafkaTopic.ReportScheduled.ToString(), ReportTypeSchedules = new List<ReportTypeSchedule>() });

            _model = new FacilityConfigModel()
            {
                FacilityId = facilityId,
                FacilityName = facilityName,
                ScheduledTasks = new List<ScheduledTaskModel>(),
                MRPCreatedDate = DateTime.Now,
                MRPModifyDate = DateTime.Now,
                CreateDate = DateTime.Now,
                ModifyDate = DateTime.Now
            };

            _serviceRegistry = new ServiceRegistry()
            {
                MeasureServiceUrl = "test"
            };

            _model.ScheduledTasks.AddRange(scheduledTaskModels);

            _linkBearerServiceOptions = new LinkBearerServiceOptions()
            {
                AllowAnonymous = true
            };

            _mocker = new AutoMocker();

            Mock<IOptions<LinkBearerServiceOptions>> _mockLinkBearerServiceOptions = _mocker.GetMock<IOptions<LinkBearerServiceOptions>>();
            _mockLinkBearerServiceOptions.Setup(p => p.Value).Returns(_linkBearerServiceOptions);

            _service = _mocker.CreateInstance<FacilityConfigurationService>();

            _ = _mocker.GetMock<IFacilityConfigurationRepo>()
                .Setup(p => p.AddAsync(_model, CancellationToken.None)).ReturnsAsync(_model);


            _ = _mocker.GetMock<IKafkaProducerFactory<string, object>>()
            .Setup(p => p.CreateAuditEventProducer(false))
            .Returns(Mock.Of<IProducer<string, AuditEventMessage>>());

            _mocker.GetMock<IOptions<MeasureConfig>>()
              .Setup(p => p.Value)
              .Returns(new MeasureConfig());

            _mocker.GetMock<IOptions<ServiceRegistry>>()
                .Setup(p => p.Value)
                .Returns(_serviceRegistry);

             await _service.CreateFacility(_model,CancellationToken.None);

            _mocker.GetMock<IFacilityConfigurationRepo>().Verify(p => p.AddAsync(_model, CancellationToken.None), Times.Once);
        }

      /*  [Fact]
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

            _serviceRegistry = new ServiceRegistry()
            {
                MeasureServiceUrl = "test"
            };

            facilities.Add(_model);

            _model.ScheduledTasks.AddRange(scheduledTaskModels);

            _mocker = new AutoMocker();

            _service = _mocker.CreateInstance<FacilityConfigurationService>();

            _ = _mocker.GetMock<IFacilityConfigurationRepo>()
                .Setup(p => p.AddAsync(_model, CancellationToken.None)).ReturnsAsync(_model);

            _mocker.GetMock<IFacilityConfigurationRepo>()
                .Setup(p => p.FirstOrDefaultAsync(x => x.FacilityId == _model.FacilityId, CancellationToken.None))
                .Returns(Task.FromResult<FacilityConfigModel>(_model));


            _ = _mocker.GetMock<IKafkaProducerFactory<string, object>>()
                .Setup(p => p.CreateAuditEventProducer(false))
                .Returns(Mock.Of<IProducer<string, AuditEventMessage>>());

            _mocker.GetMock<IOptions<MeasureConfig>>()
              .Setup(p => p.Value)
              .Returns(new MeasureConfig());

            _mocker.GetMock<IOptions<ServiceRegistry>>()
                .Setup(p => p.Value)
                .Returns(_serviceRegistry);

            _ = await Assert.ThrowsAsync<ApplicationException>(() => _service.CreateFacility(_model, CancellationToken.None));
        }*/
    }
}