using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Tenant.Business.Managers;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Controllers;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository.Context;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using System.Net;
using System.Text.Json;
using Xunit.Abstractions;
using static LantanaGroup.Link.Shared.Application.Extensions.Security.BackendAuthenticationServiceExtension;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Tenant;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class FacilityControllerTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly TenantIntegrationTestFixture _fixture;
    private readonly IServiceScope _scope;
    private readonly FacilityController _controller;
    private readonly TenantDbContext _dbContext;

    public FacilityControllerTests(TenantIntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;

        // Resolve scoped services from a per-test scope rather than the root provider, so the
        // tests pass under DI scope validation (enabled when DOTNET_ENVIRONMENT=Development).
        _scope = fixture.ServiceProvider.CreateScope();
        var sp = _scope.ServiceProvider;
        _dbContext = sp.GetRequiredService<TenantDbContext>();

        var logger = sp.GetRequiredService<ILogger<FacilityController>>();
        var scheduleService = sp.GetRequiredService<ScheduleService>();

        // The controller calls into ScheduleService, whose Quartz scheduler is only initialized in
        // StartAsync. Start it here so these tests don't depend on another test class (e.g.
        // ScheduleServiceTests) having started the shared singleton first. StartAsync is idempotent.
        scheduleService.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        var producerFactory = sp.GetRequiredService<IKafkaProducerFactory<string, GenerateReportValue>>();
        var serviceRegistry = sp.GetRequiredService<IOptions<ServiceRegistry>>();
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var linkTokenServiceConfig = sp.GetRequiredService<IOptions<LinkTokenServiceSettings>>();
        var createSystemToken = sp.GetRequiredService<ICreateSystemToken>();
        var linkBearerServiceOptions = sp.GetRequiredService<IOptions<LinkBearerServiceOptions>>();
        var queries = sp.GetRequiredService<IFacilityQueries>();
        var manager = sp.GetRequiredService<IFacilityManager>();
        var facilityOperations = sp.GetRequiredService<IFacilityOperations>();

        _controller = new FacilityController(logger, manager, queries, facilityOperations, producerFactory, serviceRegistry, httpClientFactory, linkTokenServiceConfig, createSystemToken, linkBearerServiceOptions);

        // Set HttpContext
        var httpContext = new DefaultHttpContext();
        httpContext.RequestAborted = CancellationToken.None;
        _controller.ControllerContext = new ControllerContext()
        {
            HttpContext = httpContext
        };

        Quartz.Logging.LogProvider.SetCurrentLogProvider(new NoOpQuartzLogProvider());

    }

    public void Dispose() => _scope.Dispose();

    private async Task<VendorVersion> CreateVendorVersionAsync()
    {
        var vendor = new Vendor
        {
            Id = Guid.NewGuid(),
            Name = $"Vendor-{Guid.NewGuid():N}"
        };
        var vendorVersion = new VendorVersion
        {
            Id = Guid.NewGuid(),
            VendorId = vendor.Id,
            Version = "test"
        };

        await _dbContext.Vendors.AddAsync(vendor);
        await _dbContext.VendorVersions.AddAsync(vendorVersion);
        await _dbContext.SaveChangesAsync();

        return vendorVersion;
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
        await _scope.ServiceProvider.GetRequiredService<IFacilityManager>().CreateAsync(facility, CancellationToken.None);

        var result = await _controller.GetFacilities(facilityId, facilityName, null, null, null, null, 10, 1, false, CancellationToken.None);

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
        await _scope.ServiceProvider.GetRequiredService<IFacilityManager>().CreateAsync(facility, CancellationToken.None);

        var result = await _controller.GetFacilityList(facilityId);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<Dictionary<string, string>>(okResult.Value);
    }

    [Fact]
    public async Task StoreFacility_Success()
    {
        var facilityId = Guid.NewGuid().ToString();
        var facilityName = $"Store Test {facilityId}";
        var vendorVersion = await CreateVendorVersionAsync();
        var facilityConfig = new FacilityModel
        {
            FacilityId = facilityId,
            FacilityName = facilityName,
            TimeZone = "America/Chicago",
            Vendor = new VendorModel
            {
                Id = vendorVersion.VendorId,
                Name = vendorVersion.Vendor!.Name
            },
            VendorVersionId = vendorVersion.Id,
            ScheduledReports = new TenantScheduledReportConfig { Daily = new string[] { }, Weekly = new string[] { }, Monthly = new string[] { } }
        };

        var result = await _controller.StoreFacility(facilityConfig, CancellationToken.None);
        var createdResult = Assert.IsType<CreatedResult>(result);
        var createdFacility = Assert.IsType<FacilityModel>(createdResult.Value);
        var storedFacility = await _dbContext.Facilities.AsNoTracking().SingleAsync(facility => facility.FacilityId == facilityId);

        Assert.Equal(vendorVersion.Id, storedFacility.VendorVersionId);
        Assert.Equal(vendorVersion.Id, createdFacility.VendorVersionId);
    }

    [Fact]
    public async Task PutFacility_Success()
    {
        var facilityId = Guid.NewGuid().ToString();
        var facilityName = $"Put Test {facilityId}";
        var vendorVersion = await CreateVendorVersionAsync();
        var facility = new Facility
        {
            FacilityId = facilityId,
            FacilityName = facilityName,
            TimeZone = "America/Chicago",
            ScheduledReports = new ScheduledReportModel { Daily = new string[] { }, Weekly = new string[] { }, Monthly = new string[] { } }
        };
        await _scope.ServiceProvider.GetRequiredService<IFacilityManager>().CreateAsync(facility, CancellationToken.None);

        var updateConfig = new FacilityModel
        {
            FacilityId = facilityId,
            FacilityName = "Updated Name",
            TimeZone = "America/New_York",
            Vendor = new VendorModel
            {
                Id = vendorVersion.VendorId,
                Name = vendorVersion.Vendor!.Name
            },
            VendorVersionId = vendorVersion.Id,
            ScheduledReports = new TenantScheduledReportConfig { Daily = new string[] { "NewReport" }, Weekly = new string[] { }, Monthly = new string[] { } }
        };

        var result = await _controller.PutFacility(facilityId, updateConfig, CancellationToken.None);
        var actionResult = result.Result as IActionResult;
        var objectResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var updatedFacility = Assert.IsType<FacilityModel>(objectResult.Value);
        var storedFacility = await _dbContext.Facilities.AsNoTracking().SingleAsync(facility => facility.FacilityId == facilityId);

        Assert.Equal(vendorVersion.Id, storedFacility.VendorVersionId);
        Assert.Equal(vendorVersion.Id, updatedFacility.VendorVersionId);
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
        await _scope.ServiceProvider.GetRequiredService<IFacilityManager>().CreateAsync(facility, CancellationToken.None);

        var result = await _controller.DeleteFacility(facilityId, CancellationToken.None);
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task SoftDeleteFacility_Success()
    {
        var facilityId = Guid.NewGuid().ToString();
        var facilityName = $"Soft Delete Controller Test {facilityId}";
        var facility = new Facility
        {
            FacilityId = facilityId,
            FacilityName = facilityName,
            TimeZone = "America/Chicago",
            ScheduledReports = new ScheduledReportModel { Daily = new string[] { }, Weekly = new string[] { }, Monthly = new string[] { } }
        };
        await _scope.ServiceProvider.GetRequiredService<IFacilityManager>().CreateAsync(facility, CancellationToken.None);

        var result = await _controller.SoftDeleteFacility(facilityId, CancellationToken.None);
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task SoftDeleteFacility_FacilityNotFound_ReturnsNotFound()
    {
        var nonExistentFacilityId = Guid.NewGuid().ToString();

        var result = await _controller.SoftDeleteFacility(nonExistentFacilityId, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("Not Found", notFoundResult.Value?.ToString());
    }

    [Fact]
    public async Task RestoreFacility_Success()
    {
        var facilityId = Guid.NewGuid().ToString();
        var facilityName = $"Restore Controller Test {facilityId}";
        var facility = new Facility
        {
            FacilityId = facilityId,
            FacilityName = facilityName,
            TimeZone = "America/Chicago",
            ScheduledReports = new ScheduledReportModel { Daily = new string[] { }, Weekly = new string[] { }, Monthly = new string[] { } }
        };
        var manager = _scope.ServiceProvider.GetRequiredService<IFacilityManager>();
        await manager.CreateAsync(facility, CancellationToken.None);

        // Soft delete the facility first
        await manager.SoftDeleteAsync(facilityId, CancellationToken.None);

        // Restore the facility
        var result = await _controller.RestoreFacility(facilityId, CancellationToken.None);
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task RestoreFacility_FacilityNotFound_ReturnsNotFound()
    {
        var nonExistentFacilityId = Guid.NewGuid().ToString();

        var result = await _controller.RestoreFacility(nonExistentFacilityId, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("Not Found", notFoundResult.Value?.ToString());
    }

    [Fact]
    public async Task RestoreFacility_FacilityNotDeleted_ReturnsBadRequest()
    {
        var facilityId = Guid.NewGuid().ToString();
        var facilityName = $"Restore Not Deleted Test {facilityId}";
        var facility = new Facility
        {
            FacilityId = facilityId,
            FacilityName = facilityName,
            TimeZone = "America/Chicago",
            ScheduledReports = new ScheduledReportModel { Daily = new string[] { }, Weekly = new string[] { }, Monthly = new string[] { } }
        };
        await _scope.ServiceProvider.GetRequiredService<IFacilityManager>().CreateAsync(facility, CancellationToken.None);

        // Try to restore a facility that is not deleted
        var result = await _controller.RestoreFacility(facilityId, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("is not deleted", badRequestResult.Value?.ToString());
    }

    /// <summary>
    /// The endpoints reach the scheduler through <see cref="IFacilityOperations"/>, so the jobs are the
    /// only proof the scheduling half of each operation still runs. Asserting on the response alone
    /// would pass with the scheduling dropped entirely.
    /// </summary>
    [Fact]
    public async Task Facility_lifecycle_keeps_the_scheduled_jobs_in_step()
    {
        var facilityId = Guid.NewGuid().ToString();
        var facility = new FacilityModel
        {
            FacilityId = facilityId,
            FacilityName = $"Scheduled Jobs Test {facilityId}",
            TimeZone = "America/Chicago",
            ScheduledReports = new TenantScheduledReportConfig
            {
                Daily = Array.Empty<string>(),
                Weekly = Array.Empty<string>(),
                Monthly = new[] { "NHSNAcuteCareHospitalMonthlyInitialPopulation" }
            }
        };

        Assert.IsType<CreatedResult>(await _controller.StoreFacility(facility, CancellationToken.None));
        Assert.True(await MonthlyJobExistsAsync(facilityId), "Creating a facility should schedule its jobs.");

        Assert.IsType<NoContentResult>(await _controller.SoftDeleteFacility(facilityId, CancellationToken.None));
        Assert.False(await MonthlyJobExistsAsync(facilityId), "Soft deleting a facility should remove its jobs.");

        Assert.IsType<NoContentResult>(await _controller.RestoreFacility(facilityId, CancellationToken.None));
        Assert.True(await MonthlyJobExistsAsync(facilityId), "Restoring a facility should recreate its jobs.");

        Assert.IsType<NoContentResult>(await _controller.DeleteFacility(facilityId, CancellationToken.None));
        Assert.False(await MonthlyJobExistsAsync(facilityId), "Deleting a facility should remove its jobs.");
    }

    private async Task<bool> MonthlyJobExistsAsync(string facilityId)
    {
        var scheduler = await _scope.ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();

        return await scheduler.CheckExists(
            new JobKey($"{facilityId}-{ScheduleService.MONTHLY}", nameof(KafkaTopic.ReportScheduled)));
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
        await _scope.ServiceProvider.GetRequiredService<IFacilityManager>().CreateAsync(facility, CancellationToken.None);

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
        Assert.True(response.ReportId != default);
    }

    [Fact]
    public async Task RegenerateReport_Success()
    {
        var reportId = Guid.NewGuid().ToString();
        var facilityId = Guid.NewGuid().ToString();
        var facilityName = $"Regen Test {facilityId}";
        var facility = new Facility
        {
            FacilityId = facilityId,
            FacilityName = facilityName,
            TimeZone = "America/Chicago",
            ScheduledReports = new ScheduledReportModel { Daily = new string[] { }, Weekly = new string[] { }, Monthly = new string[] { } }
        };
        await _scope.ServiceProvider.GetRequiredService<IFacilityManager>().CreateAsync(facility, CancellationToken.None);

        // Stub HttpClient response
        var handler = new StubHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(new ReportScheduleSummaryModel { FacilityId = facilityId, ReportId = reportId }))
        });
        var httpClient = new HttpClient(handler);

        var httpClientFactoryStub = new StubHttpClientFactory(httpClient);

        // Temporarily set the _httpClient to our stub factory
        typeof(FacilityController).GetField("_httpClient", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(_controller, httpClientFactoryStub);

        var request = new RegenerateReportRequest
        {
            ReportId = reportId,
            BypassSubmission = false
        };


        var result = await _controller.RegenerateReport(facilityId, request);
        var actionResult = result.Result as IActionResult;
        var objectResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateAdHocReport_EmptyFacilityId_ReturnsBadRequest(string facilityId)
    {
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
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Contains("FacilityId must be provided", badRequestResult.Value?.ToString());
    }

    [Fact]
    public async Task GenerateAdHocReport_FacilityNotFound_ReturnsNotFound()
    {
        var request = new AdHocReportRequest
        {
            ReportTypes = new List<string> { "TestReport" },
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow,
            PatientIds = new List<string>(),
            BypassSubmission = false
        };

        var result = await _controller.GenerateAdHocReport(Guid.NewGuid().ToString(), request);
        var actionResult = result.Result as IActionResult;
        Assert.IsType<NotFoundObjectResult>(actionResult);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegenerateReport_EmptyFacilityId_ReturnsBadRequest(string facilityId)
    {
        var request = new RegenerateReportRequest { ReportId = "test-report-id", BypassSubmission = false };

        var result = await _controller.RegenerateReport(facilityId, request);
        var actionResult = result.Result as IActionResult;
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Contains("FacilityId must be provided", badRequestResult.Value?.ToString());
    }

    [Fact]
    public async Task RegenerateReport_FacilityNotFound_ReturnsNotFound()
    {
        var request = new RegenerateReportRequest { ReportId = "test-report-id", BypassSubmission = false };

        var result = await _controller.RegenerateReport(Guid.NewGuid().ToString(), request);
        var actionResult = result.Result as IActionResult;
        Assert.IsType<NotFoundObjectResult>(actionResult);
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

class NoOpQuartzLogProvider : Quartz.Logging.ILogProvider
{
    public Quartz.Logging.Logger GetLogger(string name) => (level, func, exception, parameters) => true;
    public IDisposable OpenNestedContext(string message) => NoOpDisposable.Instance;
    public IDisposable OpenMappedContext(string key, object value, bool destructure = false) => NoOpDisposable.Instance;

    private class NoOpDisposable : IDisposable
    {
        public static readonly NoOpDisposable Instance = new();
        public void Dispose() { }
    }
}