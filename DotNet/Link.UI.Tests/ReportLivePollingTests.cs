using System.Reflection;
using FluentAssertions;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Link.UI.Tests;

public class ReportLivePollingTests
{
    private const string FacilityId = "05a4cf42-8920-4935-88df-695787a9ce08";
    private static readonly Guid ReportId = Guid.Parse("6b79dec7-7de6-49c0-b0b4-c933e8ca2139");

    [Fact]
    public void Active_report_with_no_progress_keeps_polling()
    {
        var page = new ReportDetailModel
        {
            Report = new FacilityReportRow { Status = ScheduleStatus.EndOfPeriod }
        };

        ReportLiveRules.ShouldRetry(page).Should().BeTrue();
        ReportLiveRules.StopPolling(page).Should().BeFalse();
    }

    [Fact]
    public void Terminal_report_stops_even_when_progress_failed()
    {
        var page = new ReportDetailModel
        {
            Report = new FacilityReportRow { Status = ScheduleStatus.Submitted }
        };

        ReportLiveRules.ShouldRetry(page).Should().BeFalse();
        ReportLiveRules.StopPolling(page).Should().BeTrue();
    }

    [Fact]
    public void Confirmed_missing_report_stops_without_a_retry()
    {
        var page = new ReportDetailModel
        {
            NotFound = true,
            LoadError = "That report was not found."
        };

        ReportLiveRules.IsTransient(page).Should().BeFalse();
        ReportLiveRules.ShouldRetry(page).Should().BeFalse();
        ReportLiveRules.StopPolling(page).Should().BeTrue();
    }

    [Fact]
    public void Unconfigured_report_service_stops_without_a_retry()
    {
        var page = new ReportDetailModel { LoadError = FacilityViewService.ReportNotConfigured };

        ReportLiveRules.IsTransient(page).Should().BeFalse();
        ReportLiveRules.ShouldRetry(page).Should().BeFalse();
        ReportLiveRules.StopPolling(page).Should().BeTrue();
    }

    [Fact]
    public async Task Transient_failure_retries_once_and_keeps_polling_when_the_second_read_fails()
    {
        var calls = 0;
        var page = await ReportLiveRules.LoadWithRetryAsync(
            _ =>
            {
                calls++;
                return Task.FromResult(new ReportDetailModel { LoadError = "Tenant returned HTTP 503." });
            },
            (_, _) => Task.CompletedTask,
            TimeSpan.Zero,
            CancellationToken.None);

        calls.Should().Be(2);
        ReportLiveRules.IsTransient(page).Should().BeTrue();
        ReportLiveRules.ShouldRetry(page).Should().BeTrue();
        ReportLiveRules.StopPolling(page).Should().BeFalse();
    }

    [Fact]
    public async Task Transient_failure_keeps_the_second_read_when_it_recovers()
    {
        var calls = 0;
        var page = await ReportLiveRules.LoadWithRetryAsync(
            _ =>
            {
                calls++;
                var failed = calls == 1;
                return Task.FromResult(new ReportDetailModel
                {
                    LoadError = failed ? "Tenant returned HTTP 503." : null,
                    Report = failed ? null : new FacilityReportRow { Status = ScheduleStatus.EndOfPeriod },
                    Progress = failed ? null : new ReportProgress { Terminal = false }
                });
            },
            (_, _) => Task.CompletedTask,
            TimeSpan.Zero,
            CancellationToken.None);

        calls.Should().Be(2);
        page.Report.Should().NotBeNull();
        ReportLiveRules.ShouldRetry(page).Should().BeFalse();
        ReportLiveRules.StopPolling(page).Should().BeFalse();
    }

    [Fact]
    public async Task Missing_report_is_not_retried()
    {
        var calls = 0;
        var page = await ReportLiveRules.LoadWithRetryAsync(
            _ =>
            {
                calls++;
                return Task.FromResult(new ReportDetailModel
                {
                    NotFound = true,
                    LoadError = "That report was not found."
                });
            },
            (_, _) => Task.CompletedTask,
            TimeSpan.Zero,
            CancellationToken.None);

        calls.Should().Be(1);
        ReportLiveRules.StopPolling(page).Should().BeTrue();
    }

    [Fact]
    public async Task Progress_failure_on_an_active_report_retries_and_keeps_polling()
    {
        var calls = 0;
        var page = await ReportLiveRules.LoadWithRetryAsync(
            _ =>
            {
                calls++;
                return Task.FromResult(new ReportDetailModel
                {
                    Report = new FacilityReportRow { Status = ScheduleStatus.EndOfPeriod }
                });
            },
            (_, _) => Task.CompletedTask,
            TimeSpan.Zero,
            CancellationToken.None);

        calls.Should().Be(2);
        page.Progress.Should().BeNull();
        ReportLiveRules.ShouldRetry(page).Should().BeTrue();
        ReportLiveRules.StopPolling(page).Should().BeFalse();
    }

    [Fact]
    public async Task Tenant_http_failure_is_a_failed_read_not_a_missing_report()
    {
        var page = await Load(facilityStatus: StatusCodes.Status503ServiceUnavailable, scheduleStatus: 200, scheduleBody: true);

        page.Report.Should().BeNull();
        page.NotFound.Should().BeFalse();
        page.LoadError.Should().Contain("Tenant");
        ReportLiveRules.IsTransient(page).Should().BeTrue();
        ReportLiveRules.StopPolling(page).Should().BeFalse();
    }

    [Fact]
    public async Task Thrown_tenant_read_keeps_polling()
    {
        var page = await Load(facilityStatus: 200, scheduleStatus: 200, scheduleBody: true, facilityThrows: true);

        page.Report.Should().BeNull();
        page.NotFound.Should().BeFalse();
        page.LoadError.Should().StartWith("Report service call failed:");
        ReportLiveRules.StopPolling(page).Should().BeFalse();
    }

    [Fact]
    public async Task Schedule_http_failure_is_a_failed_read_not_a_missing_report()
    {
        var page = await Load(facilityStatus: StatusCodes.Status200OK, scheduleStatus: StatusCodes.Status500InternalServerError, scheduleBody: false);

        page.Report.Should().BeNull();
        page.NotFound.Should().BeFalse();
        page.LoadError.Should().Contain("HTTP 500");
        ReportLiveRules.IsTransient(page).Should().BeTrue();
        ReportLiveRules.StopPolling(page).Should().BeFalse();
    }

    [Fact]
    public async Task Schedule_404_still_stops_polling()
    {
        var page = await Load(facilityStatus: StatusCodes.Status200OK, scheduleStatus: StatusCodes.Status404NotFound, scheduleBody: false);

        page.Report.Should().BeNull();
        page.NotFound.Should().BeTrue();
        page.LoadError.Should().Be("That report was not found.");
        ReportLiveRules.ShouldRetry(page).Should().BeFalse();
        ReportLiveRules.StopPolling(page).Should().BeTrue();
    }

    [Fact]
    public void Live_region_stops_only_through_the_polling_rule()
    {
        var live = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Tenants/_ReportLive.cshtml"));
        live.Should().Contain("ReportLiveRules.StopPolling");
        live.Should().NotContain("Model.Progress is null");

        var controller = File.ReadAllText(RepoFile("DotNet/Link.UI/Controllers/TenantsController.cs"));
        controller.Should().Contain("ReportLiveRules.LoadWithRetryAsync");
        controller.Should().Contain("StatusCodes.Status503ServiceUnavailable");
        controller.Should().NotContain("The report could not be read right now.");

        var js = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/live-region.js"));
        js.Should().Contain("if (!res.ok) return;");
        js.Should().Contain("clearInterval(node._auTimer)");
    }

    private static Task<ReportDetailModel> Load(int facilityStatus, int scheduleStatus, bool scheduleBody, bool facilityThrows = false)
    {
        var facilities = CallStub.Create<IFacilityServiceClient>(new Dictionary<string, Func<object?[]?, object?>>
        {
            ["GetAsync"] = _ => facilityThrows
                ? Task.FromException<LinkApiResponse<FacilityModel>>(new HttpRequestException("connection refused"))
                : Task.FromResult(new LinkApiResponse<FacilityModel>
                {
                    StatusCode = facilityStatus,
                    Body = facilityStatus == StatusCodes.Status200OK
                        ? new FacilityModel { FacilityId = FacilityId, FacilityName = "Test" }
                        : null
                })
        });
        var reports = CallStub.Create<IReportServiceClient>(new Dictionary<string, Func<object?[]?, object?>>
        {
            ["GetScheduleAsync"] = _ => Task.FromResult(new LinkApiResponse<ReportScheduleApiModel>
            {
                StatusCode = scheduleStatus,
                Body = scheduleBody
                    ? new ReportScheduleApiModel
                    {
                        Id = ReportId,
                        FacilityId = FacilityId,
                        Status = ScheduleStatus.EndOfPeriod
                    }
                    : null
            })
        });
        var service = new FacilityViewService(
            facilities,
            reports,
            acquisition: null,
            normalization: null,
            dmrp: null,
            admin: null,
            Options.Create(new LinkUiFeatureOptions()),
            NullLogger<FacilityViewService>.Instance);
        return service.LoadReportAsync(FacilityId, ReportId.ToString(), query: null, CancellationToken.None);
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
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
