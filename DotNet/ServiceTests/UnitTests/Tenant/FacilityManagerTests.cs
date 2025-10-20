using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Business.Managers;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Commands;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using static LantanaGroup.Link.Shared.Application.Extensions.Security.BackendAuthenticationServiceExtension;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Tenant
{
    [Trait("Category", "UnitTests")]
    public class FacilityManagerTests
    {
        private readonly Mock<ILogger<FacilityManager>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IEntityRepository<Facility>> _mockRepository;
        private readonly Mock<IFacilityQueries> _mockQueries;
        private readonly Mock<CreateAuditEventCommand> _mockCreateAuditEventCommand;
        private readonly Mock<ILogger<CreateAuditEventCommand>> _mockAuditCommandLogger;
        private readonly Mock<IProducer<string, AuditEventMessage>> _mockProducer;
        private readonly Mock<IOptions<ServiceRegistry>> _mockServiceRegistry;
        private readonly Mock<IOptions<MeasureConfig>> _mockMeasureConfig;
        private readonly Mock<IOptions<LinkTokenServiceSettings>> _mockLinkTokenServiceConfig;
        private readonly Mock<ICreateSystemToken> _mockCreateSystemToken;
        private readonly Mock<IOptions<LinkBearerServiceOptions>> _mockLinkBearerServiceOptions;

        private readonly FacilityManager _facilityManager;

        public FacilityManagerTests()
        {
            _mockLogger = new Mock<ILogger<FacilityManager>>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _mockRepository = new Mock<IEntityRepository<Facility>>();
            _mockQueries = new Mock<IFacilityQueries>();
            _mockAuditCommandLogger = new Mock<ILogger<CreateAuditEventCommand>>();
            _mockProducer = new Mock<IProducer<string, AuditEventMessage>>();
            _mockCreateAuditEventCommand = new Mock<CreateAuditEventCommand>(_mockAuditCommandLogger.Object, _mockProducer.Object);
            _mockServiceRegistry = new Mock<IOptions<ServiceRegistry>>();
            _mockMeasureConfig = new Mock<IOptions<MeasureConfig>>();
            _mockLinkTokenServiceConfig = new Mock<IOptions<LinkTokenServiceSettings>>();
            _mockCreateSystemToken = new Mock<ICreateSystemToken>();
            _mockLinkBearerServiceOptions = new Mock<IOptions<LinkBearerServiceOptions>>();

            var settings = new FacilityIdSettings
            {
                NumericOnlyFacilityId = false
            };

            _mockProducer.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, AuditEventMessage>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeliveryResult<string, AuditEventMessage>());

            _facilityManager = new FacilityManager(
                _mockLogger.Object,
                _httpClient,
                _mockRepository.Object,
                _mockQueries.Object,
                _mockCreateAuditEventCommand.Object,
                _mockServiceRegistry.Object,
                _mockMeasureConfig.Object,
                _mockLinkTokenServiceConfig.Object,
                _mockCreateSystemToken.Object,
                _mockLinkBearerServiceOptions.Object,
                settings);
        }

        [Fact]
        public async Task CreateAsync_SuccessfulCreation_CallsRepositoryAddAndSave()
        {
            var facility = CreateValidFacility();

            _mockRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Facility, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Facility)null);

            _mockRepository.Setup(r => r.AddAsync(It.IsAny<Facility>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(facility);

            _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockMeasureConfig.Setup(c => c.Value).Returns(new MeasureConfig { CheckIfMeasureExists = false });

            await _facilityManager.CreateAsync(facility);

            _mockRepository.Verify(r => r.AddAsync(It.Is<Facility>(f => f.FacilityId == facility.FacilityId), It.IsAny<CancellationToken>()), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_DuplicateFacility_ThrowsApplicationException()
        {
            var facility = CreateValidFacility();

            _mockRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Facility, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(facility);

            _mockMeasureConfig.Setup(c => c.Value).Returns(new MeasureConfig { CheckIfMeasureExists = false });

            await Assert.ThrowsAsync<ApplicationException>(() => _facilityManager.CreateAsync(facility));
        }

        [Fact]
        public async Task CreateAsync_MissingFacilityId_ThrowsApplicationException()
        {
            var facility = new Facility
            {
                FacilityId = "",
                FacilityName = "FacilityName",
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel { Daily = Array.Empty<string>(), Weekly = Array.Empty<string>(), Monthly = Array.Empty<string>() }
            };
            _mockRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Facility, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Facility)null);
            _mockMeasureConfig.Setup(c => c.Value).Returns(new MeasureConfig { CheckIfMeasureExists = false });
            await Assert.ThrowsAsync<ApplicationException>(() => _facilityManager.CreateAsync(facility));
        }

        [Fact]
        public async Task CreateAsync_MissingFacilityName_ThrowsApplicationException()
        {
            var facility = new Facility
            {
                FacilityId = "FacilityId",
                FacilityName = "",
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel { Daily = Array.Empty<string>(), Weekly = Array.Empty<string>(), Monthly = Array.Empty<string>() }
            };
            _mockRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Facility, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Facility)null);
            _mockMeasureConfig.Setup(c => c.Value).Returns(new MeasureConfig { CheckIfMeasureExists = false });
            await Assert.ThrowsAsync<ApplicationException>(() => _facilityManager.CreateAsync(facility));
        }

        [Fact]
        public async Task CreateAsync_InvalidTimeZone_ThrowsApplicationException()
        {
            var facility = new Facility
            {
                FacilityId = "FacilityId",
                FacilityName = "FacilityName",
                TimeZone = "InvalidTimeZone",
                ScheduledReports = new ScheduledReportModel { Daily = Array.Empty<string>(), Weekly = Array.Empty<string>(), Monthly = Array.Empty<string>() }
            };
            _mockRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Facility, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Facility)null);
            _mockMeasureConfig.Setup(c => c.Value).Returns(new MeasureConfig { CheckIfMeasureExists = false });
            await Assert.ThrowsAsync<ApplicationException>(() => _facilityManager.CreateAsync(facility));
        }

        [Fact]
        public async Task CreateAsync_DuplicateReportTypesInSchedules_ThrowsApplicationException()
        {
            var facility = CreateValidFacility();
            facility.ScheduledReports.Daily = new[] { "Report1" };
            facility.ScheduledReports.Weekly = new[] { "Report1" };

            _mockRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Facility, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Facility)null);

            _mockMeasureConfig.Setup(c => c.Value).Returns(new MeasureConfig { CheckIfMeasureExists = false });

            await Assert.ThrowsAsync<ApplicationException>(() => _facilityManager.CreateAsync(facility));
        }

        [Fact]
        public async Task CreateAsync_MeasureCheckEnabled_NonExistentMeasure_ThrowsApplicationException()
        {
            var facility = CreateValidFacility();
            facility.ScheduledReports.Daily = new[] { "InvalidReport" };

            _mockRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Facility, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Facility)null);

            _mockMeasureConfig.Setup(c => c.Value).Returns(new MeasureConfig { CheckIfMeasureExists = true });

            _mockServiceRegistry.Setup(s => s.Value).Returns(new ServiceRegistry { MeasureServiceUrl = "http://measure-service" });

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound });

            _mockLinkBearerServiceOptions.Setup(o => o.Value).Returns(new LinkBearerServiceOptions { AllowAnonymous = true });

            await Assert.ThrowsAsync<ApplicationException>(() => _facilityManager.CreateAsync(facility));
        }

        [Fact]
        public async Task UpdateAsync_SuccessfulUpdate_CallsRepositoryUpdateAndSave()
        {
            var id = Guid.NewGuid();
            var existingFacility = CreateValidFacility(id);
            var newFacility = CreateValidFacility(id);
            newFacility.FacilityName = "UpdatedName";

            _mockRepository.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingFacility);

            _mockRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Facility, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingFacility);

            _mockRepository.Setup(r => r.Update(It.IsAny<Facility>()));

            _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockMeasureConfig.Setup(c => c.Value).Returns(new MeasureConfig { CheckIfMeasureExists = false });

            var result = await _facilityManager.UpdateAsync(id, newFacility);

            Assert.Equal(id.ToString(), result);
            _mockRepository.Verify(r => r.Update(It.Is<Facility>(f => f.FacilityName == "UpdatedName")), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_FacilityNotFound_ThrowsApplicationException()
        {
            var id = Guid.NewGuid();
            var newFacility = CreateValidFacility(id);

            _mockRepository.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Facility)null);

            await Assert.ThrowsAsync<ApplicationException>(() => _facilityManager.UpdateAsync(id, newFacility));
        }

        [Fact]
        public async Task UpdateAsync_ChangeFacilityId_ThrowsApplicationException()
        {
            var id = Guid.NewGuid();
            var existingFacility = CreateValidFacility(id);
            var newFacility = CreateValidFacility(id);
            newFacility.FacilityId = "NewFacilityId";

            _mockRepository.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingFacility);

            _mockRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Facility, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingFacility);

            _mockMeasureConfig.Setup(c => c.Value).Returns(new MeasureConfig { CheckIfMeasureExists = false });

            await Assert.ThrowsAsync<ApplicationException>(() => _facilityManager.UpdateAsync(id, newFacility));
        }

        [Fact]
        public async Task UpdateAsync_DuplicateFacilityId_ThrowsApplicationException()
        {
            var id = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var existingFacility = CreateValidFacility(id);
            var otherFacility = CreateValidFacility(otherId);
            otherFacility.FacilityId = "OtherFacilityId";
            var newFacility = CreateValidFacility(id);
            newFacility.FacilityId = "OtherFacilityId";

            _mockRepository.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingFacility);

            _mockRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Facility, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(otherFacility);

            _mockMeasureConfig.Setup(c => c.Value).Returns(new MeasureConfig { CheckIfMeasureExists = false });

            await Assert.ThrowsAsync<ApplicationException>(() => _facilityManager.UpdateAsync(id, newFacility));
        }

        [Fact]
        public async Task DeleteAsync_SuccessfulDeletion_CallsRepositoryDeleteAndSave()
        {
            var facilityId = "FacilityId";
            var facility = CreateValidFacility();
            facility.FacilityId = facilityId;

            _mockRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Facility, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(facility);

            _mockRepository.Setup(r => r.Remove(It.IsAny<Facility>()));

            _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var result = await _facilityManager.DeleteAsync(facilityId);

            Assert.Equal(facilityId, result);
            _mockRepository.Verify(r => r.Remove(It.Is<Facility>(f => f.FacilityId == facilityId)), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_FacilityNotFound_ThrowsApplicationException()
        {
            var facilityId = "FacilityId";

            _mockRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Facility, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Facility)null);

            await Assert.ThrowsAsync<ApplicationException>(() => _facilityManager.DeleteAsync(facilityId));
        }

        [Fact]
        public async Task MeasureDefinitionExists_CheckDisabled_DoesNotCallHttp()
        {
            _mockMeasureConfig.Setup(c => c.Value).Returns(new MeasureConfig { CheckIfMeasureExists = false });

            await _facilityManager.MeasureDefinitionExists("ReportType");

            _mockHttpMessageHandler.Protected().Verify("SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task MeasureDefinitionExists_MissingServiceUrl_ThrowsApplicationException()
        {
            _mockMeasureConfig.Setup(c => c.Value).Returns(new MeasureConfig { CheckIfMeasureExists = true });
            _mockServiceRegistry.Setup(s => s.Value).Returns(new ServiceRegistry { MeasureServiceUrl = null });

            await Assert.ThrowsAsync<ApplicationException>(() => _facilityManager.MeasureDefinitionExists("ReportType"));
        }

        [Fact]
        public async Task MeasureDefinitionExists_SuccessfulCheck_DoesNotThrow()
        {
            _mockMeasureConfig.Setup(c => c.Value).Returns(new MeasureConfig { CheckIfMeasureExists = true });
            _mockServiceRegistry.Setup(s => s.Value).Returns(new ServiceRegistry { MeasureServiceUrl = "http://measure-service" });

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            _mockLinkBearerServiceOptions.Setup(o => o.Value).Returns(new LinkBearerServiceOptions { AllowAnonymous = true });

            await _facilityManager.MeasureDefinitionExists("ReportType");
        }

        [Fact]
        public async Task MeasureDefinitionExists_FailedCheck_ThrowsApplicationException()
        {
            _mockMeasureConfig.Setup(c => c.Value).Returns(new MeasureConfig { CheckIfMeasureExists = true });
            _mockServiceRegistry.Setup(s => s.Value).Returns(new ServiceRegistry { MeasureServiceUrl = "http://measure-service" });

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound });

            _mockLinkBearerServiceOptions.Setup(o => o.Value).Returns(new LinkBearerServiceOptions { AllowAnonymous = true });

            await Assert.ThrowsAsync<ApplicationException>(() => _facilityManager.MeasureDefinitionExists("ReportType"));
        }

        [Fact]
        public async Task MeasureDefinitionExists_NonAnonymous_AddsBearerToken()
        {
            _mockMeasureConfig.Setup(c => c.Value).Returns(new MeasureConfig { CheckIfMeasureExists = true });
            _mockServiceRegistry.Setup(s => s.Value).Returns(new ServiceRegistry { MeasureServiceUrl = "http://measure-service" });

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(req => req.Headers.Authorization != null), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            _mockLinkBearerServiceOptions.Setup(o => o.Value).Returns(new LinkBearerServiceOptions { AllowAnonymous = false });
            _mockLinkTokenServiceConfig.Setup(c => c.Value).Returns(new LinkTokenServiceSettings { SigningKey = "key" });
            _mockCreateSystemToken.Setup(t => t.ExecuteAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync("token");

            await _facilityManager.MeasureDefinitionExists("ReportType");

            _mockCreateSystemToken.Verify(t => t.ExecuteAsync("key", 2), Times.Once);
        }

        private static Facility CreateValidFacility(Guid? id = null)
        {
            return new Facility
            {
                Id = id ?? Guid.NewGuid(),
                FacilityId = "FacilityId",
                FacilityName = "FacilityName",
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel
                {
                    Daily = Array.Empty<string>(),
                    Weekly = Array.Empty<string>(),
                    Monthly = Array.Empty<string>()
                }
            };
        }
    }
}