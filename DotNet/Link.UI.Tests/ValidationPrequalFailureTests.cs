using System.Reflection;
using System.Text;
using FluentAssertions;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using Link.UI.Controllers;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Link.UI.Tests;

public class ValidationPrequalFailureTests
{
    private const string FacilityId = "05a4cf42-8920-4935-88df-695787a9ce08";
    private static readonly Guid ReportId = Guid.Parse("6b79dec7-7de6-49c0-b0b4-c933e8ca2139");

    [Fact]
    public async Task Missing_admin_bff_sets_the_not_configured_error()
    {
        var page = await Load(adminStatus: null, category: null);

        page.IssuesError.Should().Be(ReportsService.AdminBffNotConfigured);
        page.CountsUnavailable.Should().BeFalse();
        page.PrequalStatus.Should().BeNull();
    }

    [Fact]
    public async Task Missing_category_counts_leave_the_page_usable()
    {
        var page = await Load(adminStatus: StatusCodes.Status404NotFound, category: null);

        page.CountsUnavailable.Should().BeTrue();
        page.IssuesError.Should().BeNull();
        page.Unacceptable.Should().BeEmpty();
        page.PrequalStatus.Should().BeNull();
    }

    [Fact]
    public async Task A_failed_category_count_call_is_an_issue_error()
    {
        var page = await Load(adminStatus: StatusCodes.Status503ServiceUnavailable, category: null);

        page.CountsUnavailable.Should().BeFalse();
        page.IssuesError.Should().Be("Admin BFF returned HTTP 503.");
    }

    [Fact]
    public async Task A_failed_issue_page_is_an_issue_error()
    {
        var page = await Load(adminStatus: StatusCodes.Status200OK, category: "timing", issueStatus: StatusCodes.Status502BadGateway);

        page.CountsUnavailable.Should().BeFalse();
        page.Unacceptable.Should().ContainSingle().Which.Id.Should().Be("timing");
        page.IssuesError.Should().Be("Validation returned HTTP 502.");
        page.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Validation_csv_redirects_when_the_report_id_is_not_a_guid()
    {
        var (controller, http) = Controller(csvStatus: StatusCodes.Status200OK);

        var result = await controller.ValidationCsv(FacilityId, "not-a-guid", CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(ReportsController.Validation));
        controller.TempData["Error"].Should().Be("Report id is not valid.");
        http.Response.Headers.ContentDisposition.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Validation_csv_redirects_when_the_download_fails()
    {
        var (controller, _) = Controller(csvStatus: StatusCodes.Status500InternalServerError);

        var result = await controller.ValidationCsv(FacilityId, ReportId.ToString(), CancellationToken.None);

        result.Should().BeOfType<RedirectToActionResult>();
        controller.TempData["Error"].Should().Be("Validation returned HTTP 500.");
    }

    [Fact]
    public async Task Validation_csv_sets_the_download_headers_and_streams_the_body()
    {
        var (controller, http) = Controller(csvStatus: StatusCodes.Status200OK);

        var result = await controller.ValidationCsv(FacilityId, ReportId.ToString(), CancellationToken.None);

        result.Should().BeOfType<EmptyResult>();
        http.Response.ContentType.Should().Be("text/csv; charset=utf-8");
        http.Response.Headers.ContentDisposition.ToString()
            .Should().Be($"attachment; filename=\"validation-results-{ReportId:D}.csv\"");
        http.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        Encoding.UTF8.GetString(http.Response.Body is MemoryStream stream
            ? stream.ToArray()
            : []).Should().Be("resultId\n");
    }

    private static async Task<ValidationPage> Load(int? adminStatus, string? category, int issueStatus = StatusCodes.Status200OK)
    {
        var service = Service(adminStatus, issueStatus, csvStatus: StatusCodes.Status200OK);
        var query = ReportsRules.NormalizePrequalQuery(category, null, null, 1, 25);
        return await service.LoadValidationPageAsync(FacilityId, ReportId.ToString(), query, CancellationToken.None);
    }

    private static (ReportsController Controller, DefaultHttpContext Http) Controller(int csvStatus)
    {
        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();
        var controller = new ReportsController(
            Service(adminStatus: null, issueStatus: StatusCodes.Status200OK, csvStatus),
            null!,
            null!,
            Options.Create(new LinkUiFeatureOptions()),
            null!)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new EmptyTempDataProvider())
        };
        return (controller, http);
    }

    private static ReportsService Service(int? adminStatus, int issueStatus, int csvStatus)
    {
        var facilities = CallStub.Create<IFacilityServiceClient>(new Dictionary<string, Func<object?[]?, object?>>
        {
            ["GetAsync"] = _ => Task.FromResult(new LinkApiResponse<LantanaGroup.Link.Shared.Application.Models.Tenant.FacilityModel>
            {
                StatusCode = StatusCodes.Status404NotFound
            })
        });
        var reports = CallStub.Create<IReportServiceClient>(new Dictionary<string, Func<object?[]?, object?>>
        {
            ["GetScheduleAsync"] = _ => Task.FromResult(new LinkApiResponse<ReportScheduleApiModel>
            {
                StatusCode = StatusCodes.Status200OK,
                Body = new ReportScheduleApiModel
                {
                    Id = ReportId,
                    FacilityId = FacilityId,
                    Status = ScheduleStatus.Submitted
                }
            }),
            ["GetEntryCountByScheduleAsync"] = _ => Task.FromResult(new LinkApiResponse<int> { StatusCode = 200, Body = 1 }),
            ["GetInitialPopulationCountAsync"] = _ => Task.FromResult(new LinkApiResponse<int> { StatusCode = 200, Body = 1 })
        });
        var validation = CallStub.Create<IValidationServiceClient>(new Dictionary<string, Func<object?[]?, object?>>
        {
            ["GetValidationResultSummaryAsync"] = _ => Task.FromResult(new LinkApiResponse<string>
            {
                StatusCode = StatusCodes.Status404NotFound
            }),
            ["GetCategoryIssuesAsync"] = _ => Task.FromResult(new LinkApiResponse<string>
            {
                StatusCode = issueStatus,
                RawBody = issueStatus == StatusCodes.Status200OK
                    ? """{"records":[],"metadata":{"pageNumber":1,"pageSize":25,"totalCount":0,"totalPages":0}}"""
                    : null
            }),
            ["CopyValidationResultsCsvAsync"] = args =>
            {
                if (csvStatus is >= 200 and < 300)
                    ((Stream)args![2]!).Write(Encoding.UTF8.GetBytes("resultId\n"));
                return Task.FromResult(new LinkApiResponse { StatusCode = csvStatus });
            }
        });
        IAdminBffIntegrationClient? admin = adminStatus is null
            ? null
            : CallStub.Create<IAdminBffIntegrationClient>(new Dictionary<string, Func<object?[]?, object?>>
            {
                ["GetReportValidationCategoryCountsAsync"] = _ => Task.FromResult(new LinkApiResponse<string>
                {
                    StatusCode = adminStatus.Value,
                    Body = adminStatus == StatusCodes.Status200OK
                        ? """{"inProgress":false,"categories":[{"categoryId":"timing","title":"Timing","acceptable":false,"issues":4,"guidance":"Fix clocks"}]}"""
                        : null,
                    RawBody = null
                })
            });

        return new ReportsService(
            facilities,
            reports,
            submission: null,
            validation,
            measure: null,
            Options.Create(new LinkUiFeatureOptions()),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<ReportsService>.Instance,
            admin: admin);
    }

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private class CallStub : DispatchProxy
    {
        public Dictionary<string, Func<object?[]?, object?>> Methods { get; set; } = new(StringComparer.Ordinal);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is not null && Methods.TryGetValue(targetMethod.Name, out var method))
                return method(args);
            throw new NotSupportedException(targetMethod?.Name);
        }

        public static T Create<T>(Dictionary<string, Func<object?[]?, object?>> methods) where T : class
        {
            var created = DispatchProxy.Create<T, CallStub>();
            ((CallStub)(object)created).Methods = methods;
            return created;
        }
    }
}
