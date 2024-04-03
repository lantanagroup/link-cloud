using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
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

namespace TenantTests
{
    [CollectionDefinition("Non-Parallel Collection", DisableParallelization = true)]
    public class UpdateFacilityConfigurationTests
    {
        private FacilityConfigModel? _model;
        private MeasureApiConfig? _measureApiConfig;
        private const string facilityId = "TestFacility_002";
        private const string facilityName = "TestFacility_002";
        private const string id = "7241D6DA-4D15-4A41-AECC-08DC4DB45333";
        private const string id1 = "7241D6DA-4D15-4A41-AECC-08DC4DB45323";


        private AutoMocker? _mocker;
        private FacilityConfigurationService? _service;

        public ILogger<FacilityConfigurationService> logger = Mock.Of<ILogger<FacilityConfigurationService>>();

        [Fact]
        public void TestUpdateFacility()
        {

            List<ScheduledTaskModel> scheduledTaskModels = new List<ScheduledTaskModel>();
            scheduledTaskModels.Add(new ScheduledTaskModel() { KafkaTopic = KafkaTopic.ReportScheduled.ToString(), ReportTypeSchedules = new List<ReportTypeSchedule>() });
            List<FacilityConfigModel> facilities = new List<FacilityConfigModel>();

            _model = new FacilityConfigModel()
            {
                Id = new Guid(id),
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


            _model.ScheduledTasks.AddRange(scheduledTaskModels);

            _mocker = new AutoMocker();

            _service = _mocker.CreateInstance<FacilityConfigurationService>();

            _mocker.GetMock<IFacilityConfigurationRepo>()
                .Setup(p => p.UpdateAsync( _model, CancellationToken.None)).Returns(Task.FromResult<bool>(true));

            _mocker.GetMock<IFacilityConfigurationRepo>()
            .Setup(p => p.GetAsyncById(Guid.Parse(id), CancellationToken.None)).Returns(Task.FromResult<FacilityConfigModel>(_model));

            _mocker.GetMock<IFacilityConfigurationRepo>()
            .Setup(p => p.GetAsyncByFacilityId(_model.FacilityId, CancellationToken.None)).Returns(Task.FromResult(_model));

            _mocker.GetMock<IKafkaProducerFactory<string, object>>()
            .Setup(p => p.CreateAuditEventProducer())
            .Returns(Mock.Of<IProducer<string, AuditEventMessage>>());

            _mocker.GetMock<IOptions<MeasureApiConfig>>()
            .Setup(p => p.Value)
            .Returns(_measureApiConfig);

            Task<string> _updatedFacilityId = _service.UpdateFacility(Guid.Parse(id), _model, CancellationToken.None);

            _mocker.GetMock<IFacilityConfigurationRepo>().Verify(p => p.UpdateAsync(_model, CancellationToken.None), Times.Once);

            Assert.NotEmpty(_updatedFacilityId.Result);

        }



        [Fact]
        public async Task TestErrorUpdateNonExistingFacility()
        {
            List<ScheduledTaskModel> scheduledTaskModels = new List<ScheduledTaskModel>();
            scheduledTaskModels.Add(new ScheduledTaskModel() { KafkaTopic = KafkaTopic.ReportScheduled.ToString(), ReportTypeSchedules = new List<ReportTypeSchedule>() });

            _model = new FacilityConfigModel()
            {
                Id = new Guid(id),
                FacilityId = facilityId,
                FacilityName = facilityName,
                ScheduledTasks = new List<ScheduledTaskModel>(),
                MRPCreatedDate = DateTime.Now,
                MRPModifyDate = DateTime.Now
            };

            _model.ScheduledTasks.AddRange(scheduledTaskModels);

            _mocker = new AutoMocker();

            _service = _mocker.CreateInstance<FacilityConfigurationService>();

            _ = _mocker.GetMock<IFacilityConfigurationRepo>()
                .Setup(p => p.UpdateAsync(_model, CancellationToken.None)).Returns(Task.FromResult<bool>(true));

            _ = _mocker.GetMock<IFacilityConfigurationRepo>()
           .Setup(p => p.GetAsyncById(id, CancellationToken.None)).Returns(Task.FromResult<FacilityConfigModel>(result: null));

            _ = _mocker.GetMock<IFacilityConfigurationRepo>()
           .Setup(p => p.GetAsyncByFacilityId(_model.FacilityId, CancellationToken.None)).Returns(Task.FromResult(_model));

            _mocker.GetMock<IKafkaProducerFactory<string, object>>()
            .Setup(p => p.CreateAuditEventProducer())
            .Returns(Mock.Of<IProducer<string, AuditEventMessage>>());

            _ = await Assert.ThrowsAsync<ApplicationException>(() => _service.UpdateFacility(Guid.Parse(id), _model, CancellationToken.None));
        }

        [Fact]
        public async Task TestErrorDuplicateFacility()
        {
            List<ScheduledTaskModel> scheduledTaskModels = new List<ScheduledTaskModel>();

            scheduledTaskModels.Add(new ScheduledTaskModel() { KafkaTopic = KafkaTopic.ReportScheduled.ToString(), ReportTypeSchedules = new List<ReportTypeSchedule>() });

            List<FacilityConfigModel> facilities = new List<FacilityConfigModel>();

            _model = new FacilityConfigModel()
            {
                Id = new Guid(id),
                FacilityId = facilityId,
                FacilityName = facilityName,
                ScheduledTasks = new List<ScheduledTaskModel>(),
                MRPCreatedDate = DateTime.Now,
                MRPModifyDate = DateTime.Now
            };

            FacilityConfigModel _modelFound = new FacilityConfigModel()
            {
                Id = new Guid(id1),
                FacilityId = facilityId,
                FacilityName = facilityName,
                ScheduledTasks = new List<ScheduledTaskModel>(),
                MRPCreatedDate = DateTime.Now,
                MRPModifyDate = DateTime.Now
            };

            _model.ScheduledTasks.AddRange(scheduledTaskModels);

            _mocker = new AutoMocker();

            _service = _mocker.CreateInstance<FacilityConfigurationService>();

            _mocker.GetMock<IFacilityConfigurationRepo>()
                .Setup(p => p.UpdateAsync(_model, CancellationToken.None)).Returns(Task.FromResult<bool>(true));

            _mocker.GetMock<IFacilityConfigurationRepo>()
           .Setup(p => p.GetAsyncById(id, CancellationToken.None)).Returns(Task.FromResult<FacilityConfigModel>(_model));

            _mocker.GetMock<IFacilityConfigurationRepo>()
           .Setup(p => p.GetAsyncByFacilityId(_model.FacilityId, CancellationToken.None)).Returns(Task.FromResult(_modelFound));

            _mocker.GetMock<IKafkaProducerFactory<string, object>>()
            .Setup(p => p.CreateAuditEventProducer())
            .Returns(Mock.Of<IProducer<string, AuditEventMessage>>());

            _ = await Assert.ThrowsAsync<ApplicationException>(() => _service.UpdateFacility(Guid.Parse(id), _model, CancellationToken.None));
        }
    }
}
