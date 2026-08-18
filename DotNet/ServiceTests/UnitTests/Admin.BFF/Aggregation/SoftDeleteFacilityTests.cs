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
public class SoftDeleteFacilityTests
{
    private const string FacilityId = "FAC-001";

    private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private readonly IOptions<ServiceRegistry> _serviceRegistry;
    private readonly IOptions<AuthenticationSchemaConfig> _authConfig;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;

    public SoftDeleteFacilityTests()
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

    /// <summary>
    /// Builds a <see cref="MockHttpMessageHandler"/> that dispatches responses by examining
    /// the request URI path and HTTP method, then wires up real service instances around it.
    /// </summary>
    private (TenantService tenant, ReportService report, DataAcquisitionService da, CensusService census, MockHttpMessageHandler handler)
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

        return (tenant, report, da, census, handler);
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

    // ---------------------------------------------------------------------------
    // Scenario 1: All three steps succeed — returns 200
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_AllStepsSucceed_Returns200()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(request =>
        {
            // Active report schedules check: empty array
            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facilities/"))
                return OkResponse("[]");

            // Tenant soft-delete
            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/softDelete/"))
                return NoContentResponse();

            // Report schedules soft-delete
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=true"))
                return NoContentResponse();

            // DA logs soft-delete
            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/data/acquisition-logs/facility/"))
                return NoContentResponse();

            // Census jobs delete
            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/census/config/") &&
                request.RequestUri!.PathAndQuery.Contains("/jobs"))
                return NoContentResponse();

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census, _) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await SoftDeleteFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
    }

    // ---------------------------------------------------------------------------
    // Scenario 2: Active reports check returns non-empty array — returns 409
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_ActiveReportsExist_Returns409()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facilities/"))
                return OkResponse("[{\"id\":\"schedule-1\"},{\"id\":\"schedule-2\"}]");

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census, _) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await SoftDeleteFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
    }

    // ---------------------------------------------------------------------------
    // Scenario 2b: Active reports check returns success with empty array — proceeds
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_ActiveReportsCheckReturnsEmptyArray_ProceedsToSoftDelete()
    {
        // Arrange
        var tenantDeleteCalled = false;
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facilities/"))
                return OkResponse("[]");

            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/softDelete/"))
            {
                tenantDeleteCalled = true;
                return NoContentResponse();
            }

            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.Query.Contains("deleted=true"))
                return NoContentResponse();

            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/data/acquisition-logs/facility/"))
                return NoContentResponse();

            // Census jobs delete
            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/census/config/") &&
                request.RequestUri!.PathAndQuery.Contains("/jobs"))
                return NoContentResponse();

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census, _) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await SoftDeleteFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.True(tenantDeleteCalled, "Expected tenant soft-delete to be called after empty active reports.");
    }

    // ---------------------------------------------------------------------------
    // Scenario 3: Active reports check throws — returns 500
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_ActiveReportsCheckThrows_Returns500()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facilities/"))
                throw new HttpRequestException("Network error");

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census, _) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await SoftDeleteFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
    }

    // ---------------------------------------------------------------------------
    // Scenario 4: Active reports check returns non-success — proceeds without blocking
    // (non-success from the check is treated the same as "not blocking")
    // AND Tenant soft-delete returns 404 — propagates 404
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_TenantSoftDeleteReturns404_Returns404()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facilities/"))
                return OkResponse("[]");

            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/softDelete/"))
                return StatusResponse(HttpStatusCode.NotFound);

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census, _) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await SoftDeleteFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
    }

    // ---------------------------------------------------------------------------
    // Scenario 5: Tenant soft-delete throws — returns 500 (no rollback needed)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_TenantSoftDeleteThrows_Returns500()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facilities/"))
                return OkResponse("[]");

            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/softDelete/"))
                throw new HttpRequestException("Connection refused");

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census, _) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await SoftDeleteFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
    }

    // ---------------------------------------------------------------------------
    // Scenario 6: Report soft-delete fails (non-success) — returns 500 + rolls back tenant
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_ReportSoftDeleteFails_Returns500AndRollsBackTenant()
    {
        // Arrange
        var tenantRestoreCalled = false;
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facilities/"))
                return OkResponse("[]");

            // Tenant soft-delete succeeds
            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/softDelete/"))
                return NoContentResponse();

            // Report soft-delete fails
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=true"))
                return StatusResponse(HttpStatusCode.InternalServerError);

            // Tenant rollback (restore) via PATCH
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/restore/"))
            {
                tenantRestoreCalled = true;
                return NoContentResponse();
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census, _) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await SoftDeleteFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.True(tenantRestoreCalled, "Expected tenant rollback (restore) to be called when report soft-delete fails.");
    }

    // ---------------------------------------------------------------------------
    // Scenario 7: Report soft-delete throws — returns 500 + rolls back tenant
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_ReportSoftDeleteThrows_Returns500AndRollsBackTenant()
    {
        // Arrange
        var tenantRestoreCalled = false;
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facilities/"))
                return OkResponse("[]");

            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/softDelete/"))
                return NoContentResponse();

            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=true"))
                throw new HttpRequestException("Report service unavailable");

            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/restore/"))
            {
                tenantRestoreCalled = true;
                return NoContentResponse();
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census, _) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await SoftDeleteFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.True(tenantRestoreCalled, "Expected tenant rollback (restore) to be called when report soft-delete throws.");
    }

    // ---------------------------------------------------------------------------
    // Scenario 8: DA soft-delete fails (non-success) — returns 500 + rolls back both
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_DaSoftDeleteFails_Returns500AndRollsBackBoth()
    {
        // Arrange
        var tenantRestoreCalled = false;
        var reportRestoreCalled = false;
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facilities/"))
                return OkResponse("[]");

            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/softDelete/"))
                return NoContentResponse();

            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=true"))
                return NoContentResponse();

            // DA soft-delete fails
            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/data/acquisition-logs/facility/"))
                return StatusResponse(HttpStatusCode.ServiceUnavailable);

            // Report rollback (restore)
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=false"))
            {
                reportRestoreCalled = true;
                return NoContentResponse();
            }

            // Tenant rollback (restore)
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/restore/"))
            {
                tenantRestoreCalled = true;
                return NoContentResponse();
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census, _) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await SoftDeleteFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.True(reportRestoreCalled, "Expected report schedule rollback (restore) to be called when DA soft-delete fails.");
        Assert.True(tenantRestoreCalled, "Expected tenant rollback (restore) to be called when DA soft-delete fails.");
    }

    // ---------------------------------------------------------------------------
    // Scenario 9: DA soft-delete throws — returns 500 + rolls back both
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_DaSoftDeleteThrows_Returns500AndRollsBackBoth()
    {
        // Arrange
        var tenantRestoreCalled = false;
        var reportRestoreCalled = false;
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facilities/"))
                return OkResponse("[]");

            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/softDelete/"))
                return NoContentResponse();

            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=true"))
                return NoContentResponse();

            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/data/acquisition-logs/facility/"))
                throw new HttpRequestException("DA service unavailable");

            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=false"))
            {
                reportRestoreCalled = true;
                return NoContentResponse();
            }

            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/restore/"))
            {
                tenantRestoreCalled = true;
                return NoContentResponse();
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census, _) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await SoftDeleteFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.True(reportRestoreCalled, "Expected report schedule rollback (restore) to be called when DA soft-delete throws.");
        Assert.True(tenantRestoreCalled, "Expected tenant rollback (restore) to be called when DA soft-delete throws.");
    }

    // ---------------------------------------------------------------------------
    // Additional: Active reports check returns non-success HTTP — does not block
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_ActiveReportsCheckReturnsNonSuccess_DoesNotBlock()
    {
        // Arrange: the active-report pre-check returns 500 — the code only blocks
        // when the check succeeds AND returns a non-empty array, so a failed check
        // should be treated as "proceed".
        var tenantDeleteCalled = false;
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facilities/"))
                return StatusResponse(HttpStatusCode.InternalServerError);

            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/softDelete/"))
            {
                tenantDeleteCalled = true;
                return NoContentResponse();
            }

            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.Query.Contains("deleted=true"))
                return NoContentResponse();

            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/data/acquisition-logs/facility/"))
                return NoContentResponse();

            // Census jobs delete
            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/census/config/") &&
                request.RequestUri!.PathAndQuery.Contains("/jobs"))
                return NoContentResponse();

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census, _) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await SoftDeleteFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.True(tenantDeleteCalled, "Expected soft-delete to proceed when active-report check returns non-success.");
    }

    // ---------------------------------------------------------------------------
    // Additional: Tenant soft-delete returns non-404 error (e.g., 503) — propagates
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_TenantSoftDeleteReturns503_Returns503()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facilities/"))
                return OkResponse("[]");

            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/softDelete/"))
                return StatusResponse(HttpStatusCode.ServiceUnavailable);

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census, _) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await SoftDeleteFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusCode);
    }

    // ---------------------------------------------------------------------------
    // Static response helpers
    // ---------------------------------------------------------------------------

    private static HttpResponseMessage OkResponse(string jsonBody) =>
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
    // Scenario 10: Census job delete fails — returns 500 + rolls back all three
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_CensusJobDeleteFails_Returns500AndRollsBackAll()
    {
        // Arrange
        var tenantRestoreCalled = false;
        var reportRestoreCalled = false;
        var daRestoreCalled = false;
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facilities/"))
                return OkResponse("[]");

            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/softDelete/"))
                return NoContentResponse();

            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=true"))
                return NoContentResponse();

            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/data/acquisition-logs/facility/"))
                return NoContentResponse();

            // Census job delete fails
            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/census/config/") &&
                request.RequestUri!.PathAndQuery.Contains("/jobs"))
                return StatusResponse(HttpStatusCode.ServiceUnavailable);

            // DA rollback (restore)
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/data/acquisition-logs/facility/") &&
                request.RequestUri!.PathAndQuery.Contains("/restore"))
            {
                daRestoreCalled = true;
                return NoContentResponse();
            }

            // Report rollback (restore)
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=false"))
            {
                reportRestoreCalled = true;
                return NoContentResponse();
            }

            // Tenant rollback (restore)
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/restore/"))
            {
                tenantRestoreCalled = true;
                return NoContentResponse();
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census, _) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await SoftDeleteFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.True(daRestoreCalled, "Expected DA rollback (restore) to be called when census job delete fails.");
        Assert.True(reportRestoreCalled, "Expected report schedule rollback (restore) to be called when census job delete fails.");
        Assert.True(tenantRestoreCalled, "Expected tenant rollback (restore) to be called when census job delete fails.");
    }

    // ---------------------------------------------------------------------------
    // Scenario 11: Census job delete throws — returns 500 + rolls back all three
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Handle_CensusJobDeleteThrows_Returns500AndRollsBackAll()
    {
        // Arrange
        var tenantRestoreCalled = false;
        var reportRestoreCalled = false;
        var daRestoreCalled = false;
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facilities/"))
                return OkResponse("[]");

            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/softDelete/"))
                return NoContentResponse();

            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=true"))
                return NoContentResponse();

            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/data/acquisition-logs/facility/"))
                return NoContentResponse();

            // Census job delete throws
            if (request.Method == HttpMethod.Delete &&
                request.RequestUri!.PathAndQuery.Contains("api/census/config/") &&
                request.RequestUri!.PathAndQuery.Contains("/jobs"))
                throw new HttpRequestException("Census service unavailable");

            // DA rollback (restore)
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/data/acquisition-logs/facility/") &&
                request.RequestUri!.PathAndQuery.Contains("/restore"))
            {
                daRestoreCalled = true;
                return NoContentResponse();
            }

            // Report rollback (restore)
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/schedules/facility/") &&
                request.RequestUri!.Query.Contains("deleted=false"))
            {
                reportRestoreCalled = true;
                return NoContentResponse();
            }

            // Tenant rollback (restore)
            if (request.Method == HttpMethod.Patch &&
                request.RequestUri!.PathAndQuery.Contains("api/facility/restore/"))
            {
                tenantRestoreCalled = true;
                return NoContentResponse();
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (tenant, report, da, census, _) = BuildServices(handler);
        var context = BuildHttpContext();

        // Act
        var result = await SoftDeleteFacility.Handle(_loggerFactory, context, tenant, report, da, census, FacilityId);

        // Assert
        var statusCode = await ExecuteResultAsync(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.True(daRestoreCalled, "Expected DA rollback (restore) to be called when census job delete throws.");
        Assert.True(reportRestoreCalled, "Expected report schedule rollback (restore) to be called when census job delete throws.");
        Assert.True(tenantRestoreCalled, "Expected tenant rollback (restore) to be called when census job delete throws.");
    }
}

// ---------------------------------------------------------------------------
// MockHttpMessageHandler — shared within the test assembly
// ---------------------------------------------------------------------------

/// <summary>
/// A delegating <see cref="HttpMessageHandler"/> that invokes a caller-supplied
/// function for each request, allowing per-test response configuration without
/// a mocking framework.
/// </summary>
internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
}
