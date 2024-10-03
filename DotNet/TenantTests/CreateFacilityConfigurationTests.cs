
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Tenant.Commands;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Interfaces;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Repository.Interfaces.Sql;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using Moq.Protected;
using System.Linq.Expressions;
using System.Net;
using static LantanaGroup.Link.Shared.Application.Extensions.Security.BackendAuthenticationServiceExtension;

namespace TenantTests
{
    public class CreateFacilityConfigurationTests
    {
        private FacilityConfigModel? _model;
        private ServiceRegistry? _serviceRegistry;
        private LinkTokenServiceSettings? _linkTokenService;
        private MeasureConfig? _linkMeasureConfig;
        private LinkBearerServiceOptions _linkBearerServiceOptions;
        private const string facilityId = "TestFacility_002";
        private const string facilityName = "TestFacility_002";
        private List<FacilityConfigModel> facilities = new List<FacilityConfigModel>();

        private AutoMocker? _mocker;
        private IFacilityConfigurationService? _service;

        public ILogger<FacilityConfigurationService> logger = Mock.Of<ILogger<FacilityConfigurationService>>();

        private void SetUp()
        {

            _model = new FacilityConfigModel()
            {
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
                CheckIfMeasureExists = true,
            };

            _linkBearerServiceOptions = new LinkBearerServiceOptions()
            {
                AllowAnonymous = true
            };

            List<FacilityConfigModel> facilities = new List<FacilityConfigModel>();
            facilities.Add(_model);
        }


        [Fact]
        public async Task TestCreateFacility()
        {
            SetUp();

            _mocker = new AutoMocker();

            // Mock HttpMessageHandler
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync", // Method to mock
                  ItExpr.IsAny<HttpRequestMessage>(), // Any HttpRequestMessage
                  ItExpr.IsAny<CancellationToken>() // Any CancellationToken
               )
               .ReturnsAsync(new HttpResponseMessage
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent("{ 'result': 'success' }"),
               }); // Return the mocked response

            // Create HttpClient with mocked HttpMessageHandler
            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new System.Uri("http://test.com/")
            };


            // Mock IFacilityConfigurationRepo
            Mock<IFacilityConfigurationRepo> _mockFacilityRepo = _mocker.GetMock<IFacilityConfigurationRepo>();
            _mockFacilityRepo.Setup(p => p.AddAsync(_model, CancellationToken.None)).ReturnsAsync(_model);
            _ = _mockFacilityRepo.Setup(p => p.FirstOrDefaultAsync(It.IsAny<Expression<Func<FacilityConfigModel, bool>>>(), CancellationToken.None)).ReturnsAsync((FacilityConfigModel)null);

            // Mock IOptions<ServiceRegistry>
            Mock<IOptions<ServiceRegistry>> _mockServiceRegistry = _mocker.GetMock<IOptions<ServiceRegistry>>();
            _mockServiceRegistry.Setup(p => p.Value).Returns(_serviceRegistry);

            // Mock IOptions<LinkTokenServiceSettings>
            Mock<IOptions<LinkTokenServiceSettings>> _mockLinkTokenServiceSettings = _mocker.GetMock<IOptions<LinkTokenServiceSettings>>();
            _mockLinkTokenServiceSettings.Setup(p => p.Value).Returns(_linkTokenService);

            // Mock IOptions<MeasureConfig>
            Mock<IOptions<MeasureConfig>> _mockMeasureConfig = _mocker.GetMock<IOptions<MeasureConfig>>();
            _mockMeasureConfig.Setup(p => p.Value).Returns(_linkMeasureConfig);

            //Mock ILogger<CreateAuditEventCommand>
            Mock<ILogger<CreateAuditEventCommand>> _loggerMock = new Mock<ILogger<CreateAuditEventCommand>>();

            // Mock IProducer<string, object>
            Mock<IProducer<string, object>> _producerMock = new Mock<IProducer<string, object>>();
            // Inject mocked dependencies into CreateAuditEventCommand
            CreateAuditEventCommand _createAuditEventCommand = new CreateAuditEventCommand(
                _loggerMock.Object, // Inject the mocked logger
                _producerMock.Object // Inject the mocked producer
            );

            Mock<IOptions<LinkBearerServiceOptions>> _mockLinkBearerServiceOptions = _mocker.GetMock<IOptions<LinkBearerServiceOptions>>();
            _mockLinkBearerServiceOptions.Setup(p => p.Value).Returns(_linkBearerServiceOptions);

            // Create FacilityConfigurationService
            var _service = new FacilityConfigurationService(
             _mockFacilityRepo.Object,
             Mock.Of<ILogger<FacilityConfigurationService>>(),
             _createAuditEventCommand,
             _mockServiceRegistry.Object,
             _mockMeasureConfig.Object,
             httpClient,
             _mockLinkTokenServiceSettings.Object,
             Mock.Of<ICreateSystemToken>(),
             _mockLinkBearerServiceOptions.Object
             );

            // Act
            await _service.CreateFacility(_model, CancellationToken.None);

            // Assert
            _mocker.GetMock<IFacilityConfigurationRepo>().Verify(p => p.AddAsync(_model, CancellationToken.None), Times.Once);

        }



        [Fact]
        public async Task TestErrorCreateDuplicateFacility()
        {

             SetUp();

             facilities.Add(_model);

            _mocker = new AutoMocker();

            _mocker.GetMock<IFacilityConfigurationRepo>().Setup(p => p.AddAsync(_model, CancellationToken.None)).ReturnsAsync(_model);

            _mocker.GetMock<IFacilityConfigurationRepo>().Setup(p => p.FirstOrDefaultAsync(It.IsAny<Expression<Func<FacilityConfigModel, bool>>>(), CancellationToken.None)) .ReturnsAsync(_model);

            // Mock IOptions<ServiceRegistry>
            _mocker.GetMock<IOptions<MeasureConfig>>().Setup(p => p.Value).Returns(new MeasureConfig());

            // Mock IFacilityConfigurationRepo
            _mocker.GetMock<IOptions<ServiceRegistry>>().Setup(p => p.Value).Returns(_serviceRegistry);

            // Create FacilityConfigurationService
            _service = _mocker.CreateInstance<FacilityConfigurationService>();

            // Act and Assert
            var result = await _service.GetFacilityByFacilityId(facilityId, CancellationToken.None);

            // Assert
            Assert.NotNull(result);

            // Act and Assert
            _ = await Assert.ThrowsAsync<ApplicationException>(() => _service.CreateFacility(_model, CancellationToken.None));
        }
    }
}
