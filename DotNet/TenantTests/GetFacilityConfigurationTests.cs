
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Repository.Interfaces.Sql;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;


namespace TenantTests
{
    public class GetFacilityConfigurationTests
    {
        private FacilityConfigModel? _model;
        private MeasureApiConfig? _measureApiConfig;
        private const string facilityId = "TestFacility_002";
        private const string facilityName = "TestFacility_002";
        private static readonly List<ScheduledTaskModel> scheduledTaskModels = new List<ScheduledTaskModel>();
        private const string id = "7241D6DA-4D15-4A41-AECC-08DC4DB45323";

        private AutoMocker? _mocker;
        private FacilityConfigurationService? _service;

        public ILogger<FacilityConfigurationService> logger = Mock.Of<ILogger<FacilityConfigurationService>>();

        [Fact]
        public void TestGetFacility()
        {

            _model = new FacilityConfigModel()
            {
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
                .Setup(p => p.GetAsyncById(Guid.Parse(id), CancellationToken.None)).Returns(Task.FromResult<FacilityConfigModel>(_model));

            _mocker.GetMock<IOptions<MeasureApiConfig>>()
            .Setup(p => p.Value)
            .Returns(_measureApiConfig);

            Task<FacilityConfigModel> facility = _service.GetFacilityById(Guid.Parse(id), CancellationToken.None);

            _mocker.GetMock<IFacilityConfigurationRepo>().Verify(p => p.GetAsyncById(Guid.Parse(id), CancellationToken.None), Times.Once);

            Assert.NotNull(facility.Result);

        }

      /*  [Fact]
        public void TestGetFacilities()
        {

            List<FacilityConfigModel> facilities = new List<FacilityConfigModel>();

            _model = new FacilityConfigModel()
            {
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


            facilities.Add(_model);

            _model.ScheduledTasks.AddRange(scheduledTaskModels);

            _mocker = new AutoMocker();

            _service = _mocker.CreateInstance<FacilityConfigurationService>();

            _mocker.GetMock<IFacilityConfigurationRepo>()
                .Setup(p => p.GetAsync(CancellationToken.None)).Returns(Task.FromResult<List<FacilityConfigModel>>(facilities));

            _mocker.GetMock<IOptions<MeasureApiConfig>>()
             .Setup(p => p.Value)
             .Returns(_measureApiConfig);

            Task<PagedFacilityConfigModel> facilitiesResponse = _service.GetFacilities();

            _mocker.GetMock<IFacilityConfigurationRepo>().Verify(p => p.GetAsync(CancellationToken.None), Times.Once);

            Assert.NotEmpty(facilitiesResponse.Result.Records);

        }*/

    }
}