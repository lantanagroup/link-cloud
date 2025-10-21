using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Tenant.Business.Managers;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Controllers;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using Xunit.Abstractions;
using static LantanaGroup.Link.Shared.Application.Extensions.Security.BackendAuthenticationServiceExtension;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Tenant
{
    [Collection("TenantIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class FacilityControllerTests
    {
        private readonly ITestOutputHelper _output;
        private readonly TenantIntegrationTestFixture _fixture;
        private readonly FacilityController _controller;

        public FacilityControllerTests(TenantIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;

            var logger = _fixture.ServiceProvider.GetRequiredService<ILogger<FacilityController>>();
            var scheduleService = _fixture.ServiceProvider.GetRequiredService<ScheduleService>();
            var producerFactory = _fixture.ServiceProvider.GetRequiredService<IKafkaProducerFactory<string, GenerateReportValue>>();
            var serviceRegistry = _fixture.ServiceProvider.GetRequiredService<IOptions<ServiceRegistry>>();
            var httpClientFactory = _fixture.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            var linkTokenServiceConfig = _fixture.ServiceProvider.GetRequiredService<IOptions<LinkTokenServiceSettings>>();
            var createSystemToken = _fixture.ServiceProvider.GetRequiredService<ICreateSystemToken>();
            var linkBearerServiceOptions = _fixture.ServiceProvider.GetRequiredService<IOptions<LinkBearerServiceOptions>>();
            var queries = _fixture.ServiceProvider.GetRequiredService<IFacilityQueries>();
            var manager = _fixture.ServiceProvider.GetRequiredService<IFacilityManager>();

            _controller = new FacilityController(logger, manager, queries, scheduleService, producerFactory, serviceRegistry, httpClientFactory, linkTokenServiceConfig, createSystemToken, linkBearerServiceOptions);

            // Set HttpContext
            var httpContext = new DefaultHttpContext();
            httpContext.RequestAborted = CancellationToken.None;
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };
        }

        [Fact]
        public async Task GetFacilities_Success()
        {
            var facilityId = Guid.NewGuid().ToString();
            var facilityName = $"Get Facilities Test {facilityId}";
            var facility = new Facility
            {
                FacilityId = facilityId,
                FacilityName = facilityName,
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel { Daily = new string[] { }, Weekly = new string[] { }, Monthly = new string[] { } }
            };
            await _fixture.ServiceProvider.GetRequiredService<IFacilityManager>().CreateAsync(facility, CancellationToken.None);

            var result = await _controller.GetFacilities(facilityId, facilityName, null, null, 10, 1, CancellationToken.None);

            var okResult = result.Result as OkObjectResult;
            var value = okResult.Value as PagedConfigModel<FacilityModel>;
            Assert.True(value.Records.Count > 0);
        }

        [Fact]
        public async Task GetFacilityList_Success()
        {
            var facilityId = Guid.NewGuid().ToString();
            var facilityName = $"Get List Test {facilityId}";
            var facility = new Facility
            {
                FacilityId = facilityId,
                FacilityName = facilityName,
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel { Daily = new string[] { }, Weekly = new string[] { }, Monthly = new string[] { } }
            };
            await _fixture.ServiceProvider.GetRequiredService<IFacilityManager>().CreateAsync(facility, CancellationToken.None);

            var result = await _controller.GetFacilityList(facilityId);
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.IsType<Dictionary<string, string>>(okResult.Value);
        }

        [Fact]
        public async Task StoreFacility_Success()
        {
            var facilityId = Guid.NewGuid().ToString();
            var facilityName = $"Store Test {facilityId}";
            var facilityConfig = new FacilityModel
            {
                FacilityId = facilityId,
                FacilityName = facilityName,
                TimeZone = "America/Chicago",
                ScheduledReports = new TenantScheduledReportConfig { Daily = new string[] { }, Weekly = new string[] { }, Monthly = new string[] { } }
            };

            var result = await _controller.StoreFacility(facilityConfig, CancellationToken.None);
            Assert.IsType<CreatedResult>(result);
        }

        [Fact]
        public async Task PutFacility_Success()
        {
            var facilityId = Guid.NewGuid().ToString();
            var facilityName = $"Put Test {facilityId}";
            var facility = new Facility
            {
                FacilityId = facilityId,
                FacilityName = facilityName,
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel { Daily = new string[] { }, Weekly = new string[] { }, Monthly = new string[] { } }
            };
            await _fixture.ServiceProvider.GetRequiredService<IFacilityManager>().CreateAsync(facility, CancellationToken.None);

            var updateConfig = new FacilityModel
            {
                FacilityId = facilityId,
                FacilityName = "Updated Name",
                TimeZone = "America/New_York",
                ScheduledReports = new TenantScheduledReportConfig { Daily = new string[] { "NewReport" }, Weekly = new string[] { }, Monthly = new string[] { } }
            };

            var result = await _controller.PutFacility(facilityId, updateConfig, CancellationToken.None);
            var actionResult = result.Result as IActionResult;
            var objectResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.Equal(200, objectResult.StatusCode);
            Assert.IsType<FacilityModel>(objectResult.Value);
        }

        [Fact]
        public async Task DeleteFacility_Success()
        {
            var facilityId = Guid.NewGuid().ToString();
            var facilityName = $"Delete Controller Test {facilityId}";
            var facility = new Facility
            {
                FacilityId = facilityId,
                FacilityName = facilityName,
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel { Daily = new string[] { }, Weekly = new string[] { }, Monthly = new string[] { } }
            };
            await _fixture.ServiceProvider.GetRequiredService<IFacilityManager>().CreateAsync(facility, CancellationToken.None);

            var result = await _controller.DeleteFacility(facilityId, CancellationToken.None);
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task GenerateAdHocReport_Success()
        {
            var facilityId = Guid.NewGuid().ToString();
            var facilityName = $"AdHoc Test {facilityId}";
            var facility = new Facility
            {
                FacilityId = facilityId,
                FacilityName = facilityName,
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel { Daily = new string[] { }, Weekly = new string[] { }, Monthly = new string[] { } }
            };
            await _fixture.ServiceProvider.GetRequiredService<IFacilityManager>().CreateAsync(facility, CancellationToken.None);

            var request = new AdHocReportRequest
            {
                ReportTypes = new List<string> { "TestReport" },
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow,
                PatientIds = new List<string>(),
                BypassSubmission = false
            };

            var result = await _controller.GenerateAdHocReport(facilityId, request);
            var actionResult = result.Result as IActionResult;
            var objectResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.Equal(200, objectResult.StatusCode);
            var response = Assert.IsType<GenerateAdhocReportResponse>(objectResult.Value);
            Assert.NotEmpty(response.ReportId);
        }

        [Fact]
        public async Task RegenerateReport_Success()
        {
            var facilityId = Guid.NewGuid().ToString();
            var facilityName = $"Regen Test {facilityId}";
            var facility = new Facility
            {
                FacilityId = facilityId,
                FacilityName = facilityName,
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel { Daily = new string[] { }, Weekly = new string[] { }, Monthly = new string[] { } }
            };
            await _fixture.ServiceProvider.GetRequiredService<IFacilityManager>().CreateAsync(facility, CancellationToken.None);

            // Stub HttpClient response
            var handler = new StubHttpMessageHandler(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new ReportScheduleSummaryModel { FacilityId = facilityId, ReportId = "test-report-id" }))
            });
            var httpClient = new HttpClient(handler);

            var httpClientFactoryStub = new StubHttpClientFactory(httpClient);

            // Temporarily set the _httpClient to our stub factory
            typeof(FacilityController).GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(_controller, httpClientFactoryStub);

            var request = new RegenerateReportRequest
            {
                ReportId = "test-report-id",
                BypassSubmission = false
            };

            var actionResult = await _controller.RegenerateReport(facilityId, request);
            var objectResult = Assert.IsType<OkResult>(actionResult);
            Assert.Equal(200, objectResult.StatusCode);
        }

        private class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage _response;

            public StubHttpMessageHandler(HttpResponseMessage response)
            {
                _response = response;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_response);
            }
        }

        private class StubHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpClient _client;

            public StubHttpClientFactory(HttpClient client)
            {
                _client = client;
            }

            public HttpClient CreateClient(string name)
            {
                return _client;
            }
        }
    }
}