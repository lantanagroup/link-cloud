
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Repository.Interfaces.Sql;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Moq;
using Moq.AutoMock;
using System.Drawing.Printing;


namespace TenantTests
{
    public class GetFacilityConfigurationTests
    {
        private FacilityConfigModel? _model;
        private ServiceRegistry? _serviceRegistry;
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

            _serviceRegistry = new ServiceRegistry()
            {
                MeasureServiceUrl = "test"
            };

            _model.ScheduledTasks.AddRange(scheduledTaskModels);

            _mocker = new AutoMocker();

            _service = _mocker.CreateInstance<FacilityConfigurationService>();

            _mocker.GetMock<IFacilityConfigurationRepo>()
                .Setup(p => p.GetAsyncById(Guid.Parse(id), CancellationToken.None)).Returns(Task.FromResult<FacilityConfigModel>(_model));

            _mocker.GetMock<IOptions<MeasureConfig>>()
              .Setup(p => p.Value)
              .Returns(new MeasureConfig());

            _mocker.GetMock<IOptions<ServiceRegistry>>()
                .Setup(p => p.Value)
                .Returns(_serviceRegistry);

            Task<FacilityConfigModel> facility = _service.GetFacilityById(Guid.Parse(id), CancellationToken.None);

            _mocker.GetMock<IFacilityConfigurationRepo>().Verify(p => p.GetAsyncById(Guid.Parse(id), CancellationToken.None), Times.Once);

            Assert.NotNull(facility.Result);

        }

        [Fact]
        public void TestGetFacilities()
        {


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

            List<FacilityConfigModel> _facilities = new List<FacilityConfigModel>
            {
                _model
            };

            PaginationMetadata _pagedMetaData = new PaginationMetadata(10, 1, 2);

            var output = (facilities: _facilities, metadata: _pagedMetaData);

            _model.ScheduledTasks.AddRange(scheduledTaskModels);

            _mocker = new AutoMocker();

            _service = _mocker.CreateInstance<FacilityConfigurationService>();
            _ = _mocker.GetMock<IFacilityConfigurationRepo>()
                .Setup(p => p.SearchAsync("", "", "", SortOrder.Ascending, 10, 1, CancellationToken.None)).ReturnsAsync(output); 

            _mocker.GetMock<IOptions<ServiceRegistry>>()
             .Setup(p => p.Value)
             .Returns(_serviceRegistry);

            Task<PagedFacilityConfigModel> facilitiesResponse = _service.GetFacilities("","","",SortOrder.Ascending,10,1, CancellationToken.None);

            _mocker.GetMock<IFacilityConfigurationRepo>().Verify(p => p.SearchAsync("", "", "", SortOrder.Ascending, 10, 1, CancellationToken.None), Times.Once);

            Assert.NotEmpty(facilitiesResponse.Result.Records);

        }

    }
}
