using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Facility;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Admin.BFF.Aggregation;

[Trait("Category", "UnitTests")]
public class RestoreFacilityTests
{
    private const string FacilityId = "FAC-001";

    private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private readonly IOptions<ServiceRegistry> _serviceRegistry;
    private readonly IOptions<AuthenticationSchemaConfig> _authConfig;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;

    public RestoreFacilityTests()
    {
        _serviceRegistry = Options.Create(new ServiceRegistry
        {
            TenantService = new TenantServiceRegistration
            {
                TenantServiceUrl = "http://tenant/"
            },
            ReportServiceUrl = "http://report/",
            DataAcquisitionServiceUrl = "http://da/",
            CensusServiceUrl = "http://census/"
        });

        _authConfig = Options.Create(new AuthenticationSchemaConfig
        {
            EnableAnonymousAccess = true
        });

        _mockScopeFactory = new Mock<IServiceScopeFactory>();
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private (TenantService tenant, ReportService report, DataAcquisitionService da, CensusService census)
        BuildServices(MockHttpMessageHandler handler)
    {
        var tenantLogger = new Mock<ILogger<TenantService>>().Object;
        var reportLogger = new Mock<ILogger<ReportService>>().Object;
        var daLogger = new Mock<ILogger<DataAcquisitionService>>().Object;
        var censusLogger = new Mock<ILogger<CensusService>>().Object;

        var tenantClient = new HttpClient(handler) { BaseAddress = new Uri("http://tenant/") };
        var reportClient = new HttpClient(handler) { BaseAddress = new Uri("http://report/") };
        var daClient = new HttpClient(handler) { BaseAddress = new Uri("http://da/") };
        var censusClient = new HttpClient(handler) { BaseAddress = new Uri("http://census/") };

        var tenant = new TenantService(tenantLogger, tenantClient, _serviceRegistry, _authConfig, _mockScopeFactory.Object);
        var report = new ReportService(reportLogger, reportClient, _serviceRegistry, _authConfig, _mockScopeFactory.Object);
        var da = new DataAcquisitionService(daLogger, daClient, _serviceRegistry, _authConfig, _mockScopeFactory.Object);
        var census = new CensusService(censusLogger, censusClient, _serviceRegistry, _authConfig, _mockScopeFactory.Object);

        return (tenant, report, da, census);
    }

    private static DefaultHttpContext BuildHttpContext() => new();

    /// <summary>
    /// Executes an <see cref="IResult"/> against a minimal <see cref="DefaultHttpContext"/> that
    /// has <see cref="IProblemDetailsService"/> and <see cref="ILoggerFactory"/> registered,
    /// both of which are required by <c>ProblemHttpResult.ExecuteAsync</c> and
    /// <c>Ok&lt;T&gt;.ExecuteAsync</c> in ASP.NET Core 8.
    /// </summary>
    private static async Task<int> ExecuteResultAsync(IResult result)
    {
        var services = new ServiceCollection();
        services.AddProblemDetails();
        services.AddLogging();
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = provider;
        httpContext.Response.Body = new MemoryStream();
        await result.ExecuteAsync(httpContext);
        return httpContext.Response.StatusCode;
    }

    private static HttpResponseMessage OkResponse(string jsonBody = "{}") =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage NoContentResponse() =>
        new(HttpStatusCode.NoContent);

    private static HttpResponseMessage StatusResponse(HttpStatusCode statusCode) =>
        new(statusCode)
        {
            Content = new StringContent(string.Empty)
        };

    // ---------------------------------------------------------------------------
    // Scenario 1: All three steps succeed — returns 200
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_AllStepsSucceed_Returns200()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(request =>
        {
            // Tenant restore
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/restore/"))
                return NoContentResponse();

            // Report schedules restore
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=false"))
                return NoContentResponse();

            // DA logs restore
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/data/acquisition-logs/facility/") &&
                request.RequestUri!.PathAndQuery.Contains("/restore"))
                return NoContentResponse();

            // Census jobs restore
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/census/config/") &&
                request.RequestUri!.PathAndQuery.Contains("/jobs/restore"))
                return NoContentResponse();

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await RestoreFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
    }

    // ---------------------------------------------------------------------------
    // Scenario 2: Tenant restore returns 404 — propagates 404
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_TenantRestoreReturns404_Returns404()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/restore/"))
                return StatusResponse(HttpStatusCode.NotFound);

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await RestoreFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
    }

    // ---------------------------------------------------------------------------
    // Scenario 3: Tenant restore throws — returns 500 (no rollback needed)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_TenantRestoreThrows_Returns500()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/restore/"))
                throw new HttpRequestException("Tenant service unavailable");

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await RestoreFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
    }

    // ---------------------------------------------------------------------------
    // Scenario 4: Report restore fails (non-success) — returns 500 + rolls back tenant
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_ReportRestoreFails_Returns500AndRollsBackTenant()
    {
        // Arrange
        var tenantSoftDeleteCalled = false;
        var handler = new MockHttpMessageHandler(request =>
        {
            // Tenant restore succeeds
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/restore/"))
                return NoContentResponse();

            // Report schedules restore fails
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=false"))
                return StatusResponse(HttpStatusCode.InternalServerError);

            // Tenant rollback (soft-delete)
            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/softDelete/"))
            {
                tenantSoftDeleteCalled = true;
                return NoContentResponse();
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await RestoreFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.True(tenantSoftDeleteCalled, "Expected tenant rollback (soft-delete) to be called when report restore fails.");
    }

    // ---------------------------------------------------------------------------
    // Scenario 5: Report restore throws — returns 500 + rolls back tenant
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_ReportRestoreThrows_Returns500AndRollsBackTenant()
    {
        // Arrange
        var tenantSoftDeleteCalled = false;
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/restore/"))
                return NoContentResponse();

            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=false"))
                throw new HttpRequestException("Report service unavailable");

            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/softDelete/"))
            {
                tenantSoftDeleteCalled = true;
                return NoContentResponse();
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await RestoreFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.True(tenantSoftDeleteCalled, "Expected tenant rollback (soft-delete) to be called when report restore throws.");
    }

    // ---------------------------------------------------------------------------
    // Scenario 6: DA restore fails (non-success) — returns 500 + rolls back both
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_DaRestoreFails_Returns500AndRollsBackBoth()
    {
        // Arrange
        var tenantSoftDeleteCalled = false;
        var reportSoftDeleteCalled = false;
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/restore/"))
                return NoContentResponse();

            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=false"))
                return NoContentResponse();

            // DA restore fails
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/data/acquisition-logs/facility/") &&
                request.RequestUri!.PathAndQuery.Contains("/restore"))
                return StatusResponse(HttpStatusCode.ServiceUnavailable);

            // Report rollback (soft-delete)
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=true"))
            {
                reportSoftDeleteCalled = true;
                return NoContentResponse();
            }

            // Tenant rollback (soft-delete)
            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/softDelete/"))
            {
                tenantSoftDeleteCalled = true;
                return NoContentResponse();
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await RestoreFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.True(reportSoftDeleteCalled, "Expected report schedule rollback (soft-delete) to be called when DA restore fails.");
        Assert.True(tenantSoftDeleteCalled, "Expected tenant rollback (soft-delete) to be called when DA restore fails.");
    }

    // ---------------------------------------------------------------------------
    // Scenario 7: DA restore throws — returns 500 + rolls back both
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_DaRestoreThrows_Returns500AndRollsBackBoth()
    {
        // Arrange
        var tenantSoftDeleteCalled = false;
        var reportSoftDeleteCalled = false;
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/restore/"))
                return NoContentResponse();

            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=false"))
                return NoContentResponse();

            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/data/acquisition-logs/facility/") &&
                request.RequestUri!.PathAndQuery.Contains("/restore"))
                throw new HttpRequestException("DA service unavailable");

            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=true"))
            {
                reportSoftDeleteCalled = true;
                return NoContentResponse();
            }

            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/softDelete/"))
            {
                tenantSoftDeleteCalled = true;
                return NoContentResponse();
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await RestoreFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.True(reportSoftDeleteCalled, "Expected report schedule rollback (soft-delete) to be called when DA restore throws.");
        Assert.True(tenantSoftDeleteCalled, "Expected tenant rollback (soft-delete) to be called when DA restore throws.");
    }

    // ---------------------------------------------------------------------------
    // Additional: Tenant restore returns non-404 error — propagates status code
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_TenantRestoreReturns503_Returns503()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/restore/"))
                return StatusResponse(HttpStatusCode.ServiceUnavailable);

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await RestoreFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusCode);
    }

    // ---------------------------------------------------------------------------
    // Additional: Verify no DA call is made when tenant restore fails
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_TenantRestoreFails_DoesNotCallDownstreamServices()
    {
        // Arrange
        var reportCalled = false;
        var daCalled = false;
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/restore/"))
                return StatusResponse(HttpStatusCode.NotFound);

            if (request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/"))
            {
                reportCalled = true;
                return NoContentResponse();
            }

            if (request.RequestUri!.PathAndQuery.Contains("api/data/acquisition-logs/"))
            {
                daCalled = true;
                return NoContentResponse();
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        await RestoreFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        Assert.False(reportCalled, "Report service should not be called when tenant restore fails.");
        Assert.False(daCalled, "DA service should not be called when tenant restore fails.");
    }

    // ---------------------------------------------------------------------------
    // Scenario 8: Census restore fails — returns 500 + rolls back all three
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_CensusRestoreFails_Returns500AndRollsBackAll()
    {
        // Arrange
        var tenantSoftDeleteCalled = false;
        var reportSoftDeleteCalled = false;
        var daSoftDeleteCalled = false;
        var handler = new MockHttpMessageHandler(request =>
        {
            // Tenant restore succeeds
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/restore/"))
                return NoContentResponse();

            // Report restore succeeds
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=false"))
                return NoContentResponse();

            // DA restore succeeds
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/data/acquisition-logs/facility/") &&
                request.RequestUri!.PathAndQuery.Contains("/restore"))
                return NoContentResponse();

            // Census restore fails
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/census/config/") &&
                request.RequestUri!.PathAndQuery.Contains("/jobs/restore"))
                return StatusResponse(HttpStatusCode.ServiceUnavailable);

            // DA rollback (soft-delete)
            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/data/acquisition-logs/facility/"))
            {
                daSoftDeleteCalled = true;
                return NoContentResponse();
            }

            // Report rollback (soft-delete)
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=true"))
            {
                reportSoftDeleteCalled = true;
                return NoContentResponse();
            }

            // Tenant rollback (soft-delete)
            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/softDelete/"))
            {
                tenantSoftDeleteCalled = true;
                return NoContentResponse();
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await RestoreFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.True(daSoftDeleteCalled, "Expected DA rollback (soft-delete) to be called when census restore fails.");
        Assert.True(reportSoftDeleteCalled, "Expected report schedule rollback (soft-delete) to be called when census restore fails.");
        Assert.True(tenantSoftDeleteCalled, "Expected tenant rollback (soft-delete) to be called when census restore fails.");
    }

    // ---------------------------------------------------------------------------
    // Scenario 9: Census restore throws — returns 500 + rolls back all three
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_CensusRestoreThrows_Returns500AndRollsBackAll()
    {
        // Arrange
        var tenantSoftDeleteCalled = false;
        var reportSoftDeleteCalled = false;
        var daSoftDeleteCalled = false;
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/restore/"))
                return NoContentResponse();

            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=false"))
                return NoContentResponse();

            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/data/acquisition-logs/facility/") &&
                request.RequestUri!.PathAndQuery.Contains("/restore"))
                return NoContentResponse();

            // Census restore throws
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/census/config/") &&
                request.RequestUri!.PathAndQuery.Contains("/jobs/restore"))
                throw new HttpRequestException("Census service unavailable");

            // DA rollback (soft-delete)
            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/data/acquisition-logs/facility/"))
            {
                daSoftDeleteCalled = true;
                return NoContentResponse();
            }

            // Report rollback (soft-delete)
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=true"))
            {
                reportSoftDeleteCalled = true;
                return NoContentResponse();
            }

            // Tenant rollback (soft-delete)
            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/softDelete/"))
            {
                tenantSoftDeleteCalled = true;
                return NoContentResponse();
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await RestoreFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.True(daSoftDeleteCalled, "Expected DA rollback (soft-delete) to be called when census restore throws.");
        Assert.True(reportSoftDeleteCalled, "Expected report schedule rollback (soft-delete) to be called when census restore throws.");
        Assert.True(tenantSoftDeleteCalled, "Expected tenant rollback (soft-delete) to be called when census restore throws.");
    }
}
