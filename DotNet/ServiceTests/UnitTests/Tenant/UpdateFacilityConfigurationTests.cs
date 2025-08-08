using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using System.Linq.Expressions;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Tenant
{
    [Trait("Category", "UnitTests")]
    public class UpdateFacilityConfigurationTests
    {
        private Facility? _model;
        private ServiceRegistry? _serviceRegistry;
        private LinkTokenServiceSettings? _linkTokenService;
        private MeasureConfig? _linkMeasureConfig;
        private const string facilityId = "TestFacility_002";
        private const string facilityName = "TestFacility_002";
        private Guid id = Guid.Parse("7241D6DA-4D15-4A41-AECC-08DC4DB45333");
        private Guid id1 = Guid.Parse("7241D6DA-4D15-4A41-AECC-08DC4DB45323");
        private List<Facility> facilities = new List<Facility>();

        private AutoMocker? _mocker;
        private FacilityConfigurationService? _service;

        public ILogger<FacilityConfigurationService> logger = Mock.Of<ILogger<FacilityConfigurationService>>();


        private void SetUp()
        {

            _model = new Facility()
            {
                Id = id,
                FacilityId = facilityId,
                FacilityName = facilityName,
                ScheduledReports = new ScheduledReportModel(),
                TimeZone = "America/New_York",
                CreateDate = DateTime.Now,
                ModifyDate = DateTime.Now
            };

            _model.ScheduledReports.Daily = new string[] { "NHSNdQMAcuteCareHospitalInitialPopulation" };
            _model.ScheduledReports.Weekly = new string[] { "NHSNRespiratoryPathogenSurveillanceInitialPopulation" };
            _model.ScheduledReports.Monthly = new string[] { "NHSNGlycemicControlHypoglycemicInitialPopulation" };

            _serviceRegistry = new ServiceRegistry()
            {
                MeasureServiceUrl = "http://localhost:5678"
            };

            _linkTokenService = new LinkTokenServiceSettings()
            {
                SigningKey = "test"
            };


            _linkMeasureConfig = new MeasureConfig()
            {
                CheckIfMeasureExists = false,
            };

            List<LantanaGroup.Link.Tenant.Entities.Facility> facilities = new List<Facility>();
            facilities.Add(_model);
        }


        [Fact]
        public void TestUpdateFacility()
        {

            SetUp();

            _mocker = new AutoMocker();

            _service = _mocker.CreateInstance<FacilityConfigurationService>();

            _mocker.GetMock<IEntityRepository<Facility>>().Setup(p => p.UpdateAsync(_model, CancellationToken.None)).Returns(Task.FromResult<Facility>(_model));

            _mocker.GetMock<IEntityRepository<Facility>>().Setup(p => p.GetAsync(id, CancellationToken.None)).Returns(Task.FromResult<Facility>(_model));

            _mocker.GetMock<IEntityRepository<Facility>>().Setup<Task<LantanaGroup.Link.Tenant.Entities.Facility>>(p => p.FirstOrDefaultAsync(It.IsAny<Expression<Func<Facility, bool>>>(), CancellationToken.None)).Returns(Task.FromResult(_model));

            _mocker.GetMock<IOptions<MeasureConfig>>().Setup(p => p.Value).Returns(new MeasureConfig() { CheckIfMeasureExists = false });

            _mocker.GetMock<IOptions<ServiceRegistry>>().Setup(p => p.Value).Returns(_serviceRegistry);

            _mocker.GetMock<IOptions<LinkTokenServiceSettings>>().Setup(p => p.Value).Returns(_linkTokenService);

            Task<string> _updatedFacilityId = _service.UpdateFacility(id, _model, CancellationToken.None);

            _mocker.GetMock<IEntityRepository<Facility>>().Verify(p => p.UpdateAsync(_model, CancellationToken.None), Times.Once);

            Assert.NotEmpty(_updatedFacilityId.Result);

        }

        [Fact]
        public async Task TestErrorUpdateNonExistingFacility()
        {

            SetUp();

            _mocker = new AutoMocker();

            _service = _mocker.CreateInstance<FacilityConfigurationService>();

            _mocker.GetMock<IEntityRepository<Facility>>().Setup(p => p.UpdateAsync(_model, CancellationToken.None)).Returns(Task.FromResult<Facility>(_model));

            _mocker.GetMock<IEntityRepository<Facility>>().Setup(p => p.GetAsync(id, CancellationToken.None)).Returns(Task.FromResult<Facility>(result: null));

            _mocker.GetMock<IEntityRepository<Facility>>().Setup(p => p.FirstOrDefaultAsync(It.IsAny<Expression<Func<Facility, bool>>>(), CancellationToken.None)).ReturnsAsync((Facility)null);

            _mocker.GetMock<IOptions<MeasureConfig>>().Setup(p => p.Value).Returns(new MeasureConfig() { CheckIfMeasureExists = false });

            Task<string> _updatedFacilityId = _service.UpdateFacility(id, _model, CancellationToken.None);

            _mocker.GetMock<IEntityRepository<Facility>>().Verify(p => p.AddAsync(_model, CancellationToken.None), Times.Once);

        }

        [Fact]
        public async Task TestErrorDuplicateFacility()
        {

            Facility _modelFound = new Facility()
            {
                Id = id1,
                FacilityId = facilityId,
                FacilityName = facilityName,
                TimeZone = "America/New_York"
            };

            SetUp();

            facilities.Add(_model);

            _mocker = new AutoMocker();

            _service = _mocker.CreateInstance<FacilityConfigurationService>();

            _mocker.GetMock<IEntityRepository<Facility>>().Setup(p => p.UpdateAsync(_model, CancellationToken.None)).Returns(Task.FromResult<Facility>(_model));

            _mocker.GetMock<IEntityRepository<Facility>>().Setup(p => p.GetAsync(id, CancellationToken.None)).Returns(Task.FromResult<Facility>(_model));

            _mocker.GetMock<IEntityRepository<Facility>>().Setup(p => p.FirstOrDefaultAsync(It.IsAny<Expression<Func<Facility, bool>>>(), CancellationToken.None)).ReturnsAsync(_modelFound);

            await Assert.ThrowsAsync<ApplicationException>(() => _service.UpdateFacility(id, _model, CancellationToken.None));
        }

    }
}
