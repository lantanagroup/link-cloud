using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Validation;
using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Report;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
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
public class GetReportValidationCategoryCountsTests
{
    private const string ReportId = "22222222-2222-2222-2222-222222222222";
    private const string FacilityId = "facility-1";
    private const string ScheduleJson = "{\"id\":\"" + ReportId + "\",\"facilityId\":\"" + FacilityId + "\"}";
    private const string CountsJson =
        "{\"facilityId\":\"" + FacilityId + "\",\"reportId\":\"" + ReportId + "\"," +
        "\"lastUpdated\":\"2026-10-21T12:00:00Z\",\"categories\":[" +
        "{\"categoryId\":\"category-a\",\"title\":\"Category A\",\"acceptable\":false,\"submit\":true,\"issues\":7}," +
        "{\"categoryId\":\"uncategorized\",\"title\":\"Uncategorized\",\"acceptable\":false,\"submit\":true," +
        "\"issues\":3}," +
        "{\"categoryId\":\"deleted-category\",\"title\":\"Deleted\",\"acceptable\":true,\"submit\":false," +
        "\"issues\":1}]}";
    private const string CategoriesJson =
        "[{\"id\":\"CATEGORY-A\",\"title\":\"Category A\",\"severity\":\"ERROR\",\"guidance\":\"Fix the code.\"}," +
        "{\"id\":\"category-b\",\"title\":\"Category B\",\"severity\":\"WARNING\",\"guidance\":\"Review.\"}]";

    private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private readonly IOptions<ServiceRegistry> _serviceRegistry;
    private readonly IOptions<AuthenticationSchemaConfig> _authConfig;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;

    public GetReportValidationCategoryCountsTests()
    {
        _serviceRegistry = Options.Create(new ServiceRegistry
        {
            ReportServiceUrl = "http://report/",
            ValidationServiceUrl = "http://validation/"
        });
        _authConfig = Options.Create(new AuthenticationSchemaConfig { EnableAnonymousAccess = true });
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
    }

    [Fact]
    public async Task Handle_InvalidId_ReturnsBadRequest()
    {
        var handler = new MockHttpMessageHandler(request =>
            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}"));
        var (report, validation) = BuildServices(handler);

        var result = await GetReportValidationCategoryCounts.Handle(
            _loggerFactory, new DefaultHttpContext(), report, validation, "not-a-guid");

        Assert.Equal(StatusCodes.Status400BadRequest, await ExecuteResultAsync(result));
    }

    [Fact]
    public async Task Handle_ScheduleNotFound_ReturnsNotFoundWithoutCallingValidation()
    {
        var validationCalled = false;
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.RequestUri!.Host == "validation")
            {
                validationCalled = true;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var (report, validation) = BuildServices(handler);

        var result = await GetReportValidationCategoryCounts.Handle(
            _loggerFactory, new DefaultHttpContext(), report, validation, ReportId);

        Assert.Equal(StatusCodes.Status404NotFound, await ExecuteResultAsync(result));
        Assert.False(validationCalled);
    }

    [Fact]
    public async Task Handle_CountsUnavailable_ReturnsNotFound()
    {
        var handler = new MockHttpMessageHandler(request => request.RequestUri!.Host == "report"
            ? OkJson(ScheduleJson)
            : new HttpResponseMessage(HttpStatusCode.NotFound));
        var (report, validation) = BuildServices(handler);

        var result = await GetReportValidationCategoryCounts.Handle(
            _loggerFactory, new DefaultHttpContext(), report, validation, ReportId);

        Assert.Equal(StatusCodes.Status404NotFound, await ExecuteResultAsync(result));
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Contains("unavailable", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task Handle_CountsFound_AddsGuidanceAndSeverityByCategoryId()
    {
        string? countsPath = null;
        var handler = new MockHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.PathAndQuery;
            if (request.RequestUri.Host == "report" && path.Contains($"api/schedules/{ReportId}"))
            {
                return OkJson(ScheduleJson);
            }
            if (path.Contains("/category-counts"))
            {
                countsPath = path;
                return OkJson(CountsJson);
            }
            if (path.EndsWith("api/validation/category"))
            {
                return OkJson(CategoriesJson);
            }
            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });
        var (report, validation) = BuildServices(handler);

        var result = await GetReportValidationCategoryCounts.Handle(
            _loggerFactory, new DefaultHttpContext(), report, validation, ReportId, submit: true);

        var counts = Assert.IsType<Ok<ReportValidationCategoryCounts>>(result).Value!;
        Assert.Equal($"/api/validation/facilities/{FacilityId}/reports/{ReportId}/category-counts?submit=true",
                     countsPath);
        Assert.Equal(ReportId, counts.ReportScheduleId);
        Assert.Equal(FacilityId, counts.FacilityId);
        Assert.Equal(DateTimeOffset.Parse("2026-10-21T12:00:00Z"), counts.LastUpdated);
        Assert.Equal(new[] { "category-a", "uncategorized", "deleted-category" },
                     counts.Categories.Select(c => c.CategoryId));

        var categoryA = counts.Categories[0];
        Assert.Equal("ERROR", categoryA.Severity);
        Assert.Equal("Fix the code.", categoryA.Guidance);
        Assert.Equal(7, categoryA.Issues);
        Assert.True(categoryA.Submit);

        Assert.Null(counts.Categories[1].Guidance);
        Assert.Null(counts.Categories[2].Severity);
        Assert.False(counts.Categories[2].Submit);
    }

    [Fact]
    public async Task Handle_SubmitOmitted_SendsNoFilter()
    {
        string? countsPath = null;
        var handler = new MockHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.PathAndQuery;
            if (request.RequestUri.Host == "report")
            {
                return OkJson(ScheduleJson);
            }
            if (path.Contains("/category-counts"))
            {
                countsPath = path;
                return OkJson(CountsJson);
            }
            return OkJson(CategoriesJson);
        });
        var (report, validation) = BuildServices(handler);

        await GetReportValidationCategoryCounts.Handle(
            _loggerFactory, new DefaultHttpContext(), report, validation, ReportId);

        Assert.Equal($"/api/validation/facilities/{FacilityId}/reports/{ReportId}/category-counts", countsPath);
    }

    [Fact]
    public async Task Handle_CategoryListFails_StillReturnsCountsWithoutGuidance()
    {
        var handler = new MockHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.PathAndQuery;
            if (request.RequestUri.Host == "report")
            {
                return OkJson(ScheduleJson);
            }
            return path.Contains("/category-counts")
                ? OkJson(CountsJson)
                : new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });
        var (report, validation) = BuildServices(handler);

        var result = await GetReportValidationCategoryCounts.Handle(
            _loggerFactory, new DefaultHttpContext(), report, validation, ReportId);

        var counts = Assert.IsType<Ok<ReportValidationCategoryCounts>>(result).Value!;
        Assert.Equal(3, counts.Categories.Count);
        Assert.All(counts.Categories, c => Assert.Null(c.Guidance));
    }

    [Fact]
    public async Task Handle_NoCategories_DoesNotRequestTheCategoryList()
    {
        var categoryListRequested = false;
        var handler = new MockHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.PathAndQuery;
            if (request.RequestUri.Host == "report")
            {
                return OkJson(ScheduleJson);
            }
            if (path.Contains("/category-counts"))
            {
                return OkJson("{\"facilityId\":\"" + FacilityId + "\",\"reportId\":\"" + ReportId + "\"," +
                              "\"lastUpdated\":\"2026-10-21T12:00:00Z\",\"categories\":[]}");
            }
            categoryListRequested = true;
            return OkJson(CategoriesJson);
        });
        var (report, validation) = BuildServices(handler);

        var result = await GetReportValidationCategoryCounts.Handle(
            _loggerFactory, new DefaultHttpContext(), report, validation, ReportId);

        var counts = Assert.IsType<Ok<ReportValidationCategoryCounts>>(result).Value!;
        Assert.Empty(counts.Categories);
        Assert.False(categoryListRequested);
    }

    [Fact]
    public async Task Handle_ValidationUnauthorized_ReturnsUnauthorized()
    {
        var handler = new MockHttpMessageHandler(request => request.RequestUri!.Host == "report"
            ? OkJson(ScheduleJson)
            : new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var (report, validation) = BuildServices(handler);

        var result = await GetReportValidationCategoryCounts.Handle(
            _loggerFactory, new DefaultHttpContext(), report, validation, ReportId);

        Assert.Equal(StatusCodes.Status401Unauthorized, await ExecuteResultAsync(result));
    }

    [Fact]
    public async Task Handle_ScheduleWithNoFacility_ReturnsBadGateway()
    {
        var handler = new MockHttpMessageHandler(request => request.RequestUri!.Host == "report"
            ? OkJson("{\"id\":\"" + ReportId + "\",\"facilityId\":\"\"}")
            : throw new InvalidOperationException($"Unexpected request: {request.RequestUri}"));
        var (report, validation) = BuildServices(handler);

        var result = await GetReportValidationCategoryCounts.Handle(
            _loggerFactory, new DefaultHttpContext(), report, validation, ReportId);

        Assert.Equal(StatusCodes.Status502BadGateway, await ExecuteResultAsync(result));
    }

    [Fact]
    public async Task Handle_ValidationReturnsNoCountsBody_ReturnsBadGateway()
    {
        var handler = new MockHttpMessageHandler(request => request.RequestUri!.Host == "report"
            ? OkJson(ScheduleJson)
            : OkJson("null"));
        var (report, validation) = BuildServices(handler);

        var result = await GetReportValidationCategoryCounts.Handle(
            _loggerFactory, new DefaultHttpContext(), report, validation, ReportId);

        Assert.Equal(StatusCodes.Status502BadGateway, await ExecuteResultAsync(result));
    }

    [Fact]
    public async Task Handle_ValidationForbidden_ReturnsForbid()
    {
        var handler = new MockHttpMessageHandler(request => request.RequestUri!.Host == "report"
            ? OkJson(ScheduleJson)
            : new HttpResponseMessage(HttpStatusCode.Forbidden));
        var (report, validation) = BuildServices(handler);

        var result = await GetReportValidationCategoryCounts.Handle(
            _loggerFactory, new DefaultHttpContext(), report, validation, ReportId);

        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task Handle_ValidationUnavailable_PassesTheStatusThrough()
    {
        var handler = new MockHttpMessageHandler(request => request.RequestUri!.Host == "report"
            ? OkJson(ScheduleJson)
            : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var (report, validation) = BuildServices(handler);

        var result = await GetReportValidationCategoryCounts.Handle(
            _loggerFactory, new DefaultHttpContext(), report, validation, ReportId);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, await ExecuteResultAsync(result));
    }

    [Fact]
    public async Task Handle_UnexpectedException_ReturnsInternalServerError()
    {
        var handler = new MockHttpMessageHandler(request => request.RequestUri!.Host == "report"
            ? OkJson(ScheduleJson)
            : throw new HttpRequestException("connection refused"));
        var (report, validation) = BuildServices(handler);

        var result = await GetReportValidationCategoryCounts.Handle(
            _loggerFactory, new DefaultHttpContext(), report, validation, ReportId);

        Assert.Equal(StatusCodes.Status500InternalServerError, await ExecuteResultAsync(result));
    }

    private (ReportService report, ValidationService validation) BuildServices(MockHttpMessageHandler handler)
    {
        var report = new ReportService(
            new Mock<ILogger<ReportService>>().Object,
            new HttpClient(handler),
            _serviceRegistry,
            _authConfig,
            _mockScopeFactory.Object);
        var validation = new ValidationService(
            new Mock<ILogger<ValidationService>>().Object,
            new HttpClient(handler),
            _serviceRegistry,
            _authConfig,
            _mockScopeFactory.Object);
        return (report, validation);
    }

    private static async Task<int> ExecuteResultAsync(IResult result)
    {
        var services = new ServiceCollection();
        services.AddProblemDetails();
        services.AddLogging();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() }
        };
        await result.ExecuteAsync(httpContext);
        return httpContext.Response.StatusCode;
    }

    private static HttpResponseMessage OkJson(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
