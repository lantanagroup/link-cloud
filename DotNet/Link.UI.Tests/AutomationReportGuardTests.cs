using System.Reflection;
using FluentAssertions;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Integration.Tenant;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Link.UI.Tests;

public class AutomationReportGuardTests
{
    private const string FacilityId = "facility-1";
    private static readonly Guid ReportId = Guid.Parse("6b79dec7-7de6-49c0-b0b4-c933e8ca2139");

    [Fact]
    public async Task Resubmit_refuses_a_test_facility_before_regenerating()
    {
        var regenerated = 0;
        var reads = 0;
        var service = View(automationEnabled: true, isTest: true, () => regenerated++, () => reads++);

        var result = await service.ResubmitAsync(FacilityId, ReportId.ToString(), bypassSubmission: true, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Be(AutomationMarkRules.AdHocReportBlocked);
        regenerated.Should().Be(0);
        reads.Should().Be(1);
    }

    [Fact]
    public async Task Resubmit_still_calls_tenant_for_a_facility_the_run_does_not_own()
    {
        var regenerated = 0;
        var service = View(automationEnabled: true, isTest: false, () => regenerated++, () => { });

        var result = await service.ResubmitAsync(FacilityId, ReportId.ToString(), bypassSubmission: false, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Message.Should().Be("Report resubmitted.");
        regenerated.Should().Be(1);
    }

    [Fact]
    public async Task Resubmit_refuses_a_test_facility_when_automation_is_off()
    {
        var regenerated = 0;
        var reads = 0;
        var service = View(automationEnabled: false, isTest: true, () => regenerated++, () => reads++);

        var result = await service.ResubmitAsync(FacilityId, ReportId.ToString(), bypassSubmission: false, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Be(AutomationMarkRules.AdHocReportBlocked);
        regenerated.Should().Be(0);
        reads.Should().Be(1);
    }

    [Fact]
    public async Task Ad_hoc_generate_refuses_a_test_facility_before_generating()
    {
        var generated = 0;
        var reads = 0;
        var service = Reports(automationEnabled: true, isTest: true, () => generated++, () => reads++);

        var page = await service.GenerateAsync(new GenerateReportInput { FacilityId = FacilityId }, CancellationToken.None);

        page.Error.Should().Be(AutomationMarkRules.AdHocReportBlocked);
        page.GeneratedReportId.Should().BeNull();
        page.AutomationFacilitiesExcluded.Should().BeTrue();
        generated.Should().Be(0);
        reads.Should().Be(1);
    }

    [Fact]
    public async Task Ad_hoc_generate_keeps_going_for_a_facility_the_run_does_not_own()
    {
        var reads = 0;
        var service = Reports(automationEnabled: true, isTest: false, () => { }, () => reads++);

        var page = await service.GenerateAsync(new GenerateReportInput { FacilityId = FacilityId }, CancellationToken.None);

        page.Error.Should().Be("Choose at least one measure.");
        reads.Should().Be(1);
    }

    [Fact]
    public async Task Resubmit_refuses_when_ownership_cannot_be_read()
    {
        var regenerated = 0;
        var lookups = 0;
        var service = View(automationEnabled: true, isTest: false, () => regenerated++, () => lookups++, reachable: false);

        var result = await service.ResubmitAsync(FacilityId, ReportId.ToString(), bypassSubmission: true, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Be(AutomationMarkRules.OwnershipUnreachable);
        regenerated.Should().Be(0);
        lookups.Should().Be(1);
    }

    [Fact]
    public async Task Ad_hoc_generate_refuses_when_ownership_cannot_be_read()
    {
        var tenantCalls = 0;
        var lookups = 0;
        var service = Reports(automationEnabled: true, isTest: false, () => tenantCalls++, () => lookups++, reachable: false);

        var page = await service.GenerateAsync(new GenerateReportInput { FacilityId = FacilityId }, CancellationToken.None);

        page.Error.Should().Be(AutomationMarkRules.OwnershipUnreachable);
        page.GeneratedReportId.Should().BeNull();
        tenantCalls.Should().Be(0);
        lookups.Should().Be(1);
    }

    [Fact]
    public async Task Facility_report_rows_hide_resubmit_for_an_owned_facility()
    {
        var lookups = 0;
        var service = Listed(isTest: true, () => lookups++);

        var page = await service.LoadAsync(FacilityId, new FacilityViewQuery(), CancellationToken.None);

        page.Reports.Should().ContainSingle();
        page.Reports[0].CanResubmit.Should().BeFalse();
        lookups.Should().Be(1);
    }

    [Fact]
    public async Task Facility_page_offers_no_resubmit_when_the_facility_cannot_be_read()
    {
        var lookups = 0;
        var service = Listed(isTest: false, () => lookups++, reachable: false);

        var page = await service.LoadAsync(FacilityId, new FacilityViewQuery(), CancellationToken.None);

        page.LoadError.Should().NotBeNullOrWhiteSpace();
        page.Reports.Should().BeEmpty();
        lookups.Should().Be(1);
    }

    [Fact]
    public async Task Facility_report_rows_keep_resubmit_when_the_facility_is_not_owned()
    {
        var service = Listed(isTest: false, () => { });

        var page = await service.LoadAsync(FacilityId, new FacilityViewQuery(), CancellationToken.None);

        page.Reports.Should().ContainSingle();
        page.Reports[0].CanResubmit.Should().BeTrue();
    }

    [Fact]
    public async Task Ad_hoc_generate_refuses_a_test_facility_when_automation_is_off()
    {
        var generated = 0;
        var reads = 0;
        var service = Reports(automationEnabled: false, isTest: true, () => generated++, () => reads++);

        var page = await service.GenerateAsync(new GenerateReportInput { FacilityId = FacilityId }, CancellationToken.None);

        page.Error.Should().Be(AutomationMarkRules.AdHocReportBlocked);
        page.AutomationFacilitiesExcluded.Should().BeFalse();
        generated.Should().Be(0);
        reads.Should().Be(1);
    }

    [Fact]
    public void Resubmit_dialog_is_rendered_only_when_a_row_can_resubmit()
    {
        foreach (var relative in new[]
        {
            "DotNet/Link.UI/Views/Reports/Index.cshtml",
            "DotNet/Link.UI/Views/Tenants/_ViewReports.cshtml"
        })
        {
            var text = File.ReadAllText(RepoFile(relative));
            text.Should().Contain("Model.Reports.Any(report => report.CanResubmit)");
            text.Should().Contain("Regenerate without submitting");
        }

        var reports = File.ReadAllText(RepoFile("DotNet/Link.UI/Controllers/ReportsController.cs"));
        reports.Should().Contain("ForIdsAsync");
        reports.Should().Contain("!flags.Reachable || flags.IsTest(report.FacilityId)");

        var view = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/FacilityViewService.cs"));
        view.Should().Contain("facility.Body.IsTest");
        view.Should().Contain("OwnershipUnreachable");

        var generate = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/ReportsService.cs"));
        generate.Should().Contain("facility.Body.IsTest");
        generate.Should().Contain("OwnershipUnreachable");

        var tenants = File.ReadAllText(RepoFile("DotNet/Link.UI/Controllers/TenantsController.cs"));
        tenants.Should().Contain("item.IsTest");

        var form = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Reports/Generate.cshtml"));
        form.Should().Contain("AutomationFacilitiesExcluded");
        form.Should().Contain("Automation facilities cannot be used here.");
    }

    private static FacilityViewService View(bool automationEnabled, bool isTest, Action regenerated, Action lookedUp, bool reachable = true)
    {
        var facilities = Stub.Create<IFacilityServiceClient>(new Dictionary<string, Func<object?[]?, object?>>(StringComparer.Ordinal)
        {
            ["GetAsync"] = _ =>
            {
                lookedUp();
                if (!reachable)
                    return Task.FromResult(new LinkApiResponse<FacilityModel> { StatusCode = StatusCodes.Status500InternalServerError });
                return Task.FromResult(new LinkApiResponse<FacilityModel>
                {
                    StatusCode = StatusCodes.Status200OK,
                    Body = new FacilityModel { FacilityId = FacilityId, IsTest = isTest }
                });
            },
            ["RegenerateReportAsync"] = _ =>
            {
                regenerated();
                return Task.FromResult(new LinkApiResponse<GenerateAdhocReportResponseApiModel>
                {
                    StatusCode = StatusCodes.Status200OK,
                    Body = new GenerateAdhocReportResponseApiModel { ReportId = Guid.NewGuid() }
                });
            }
        });
        var reports = Stub.Create<IReportServiceClient>(new Dictionary<string, Func<object?[]?, object?>>(StringComparer.Ordinal)
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
            })
        });
        return new FacilityViewService(
            facilities,
            reports,
            acquisition: null,
            normalization: null,
            dmrp: null,
            admin: null,
            Options.Create(new LinkUiFeatureOptions { AutomationEnabled = automationEnabled }),
            NullLogger<FacilityViewService>.Instance);
    }

    private static FacilityViewService Listed(bool isTest, Action lookedUp, bool reachable = true)
    {
        var facilities = Stub.Create<IFacilityServiceClient>(new Dictionary<string, Func<object?[]?, object?>>(StringComparer.Ordinal)
        {
            ["GetAsync"] = _ =>
            {
                lookedUp();
                if (!reachable)
                    return Task.FromResult(new LinkApiResponse<FacilityModel> { StatusCode = StatusCodes.Status500InternalServerError });
                return Task.FromResult(new LinkApiResponse<FacilityModel>
                {
                    StatusCode = StatusCodes.Status200OK,
                    Body = new FacilityModel { FacilityId = FacilityId, FacilityName = "Owned hospital", IsTest = isTest }
                });
            }
        });
        var reports = Stub.Create<IReportServiceClient>(new Dictionary<string, Func<object?[]?, object?>>(StringComparer.Ordinal)
        {
            ["SearchFacilitySchedulesAsync"] = _ => Task.FromResult(new LinkApiResponse<PagedConfigModel<ReportScheduleApiModel>>
            {
                StatusCode = StatusCodes.Status200OK,
                Body = new PagedConfigModel<ReportScheduleApiModel>(
                    [
                        new ReportScheduleApiModel
                        {
                            Id = ReportId,
                            FacilityId = FacilityId,
                            Status = ScheduleStatus.Submitted
                        }
                    ],
                    new PaginationMetadata(10, 1, 1))
            }),
            ["GetEntryCountByScheduleAsync"] = _ => Task.FromResult(new LinkApiResponse<int> { StatusCode = StatusCodes.Status200OK, Body = 2 }),
            ["GetInitialPopulationCountAsync"] = _ => Task.FromResult(new LinkApiResponse<int> { StatusCode = StatusCodes.Status200OK, Body = 1 })
        });
        return new FacilityViewService(
            facilities,
            reports,
            acquisition: null,
            normalization: null,
            dmrp: null,
            admin: null,
            Options.Create(new LinkUiFeatureOptions { AutomationEnabled = true }),
            NullLogger<FacilityViewService>.Instance);
    }

    private static ReportsService Reports(bool automationEnabled, bool isTest, Action generated, Action read, bool reachable = true)
    {
        var facilities = Stub.Create<IFacilityServiceClient>(new Dictionary<string, Func<object?[]?, object?>>(StringComparer.Ordinal)
        {
            ["GetAsync"] = _ =>
            {
                read();
                if (!reachable)
                    return Task.FromResult(new LinkApiResponse<FacilityModel> { StatusCode = StatusCodes.Status500InternalServerError });
                return Task.FromResult(new LinkApiResponse<FacilityModel>
                {
                    StatusCode = StatusCodes.Status200OK,
                    Body = new FacilityModel { FacilityId = FacilityId, IsTest = isTest, TimeZone = "America/Chicago" }
                });
            },
            ["GenerateAdhocReportAsync"] = _ =>
            {
                generated();
                throw new InvalidOperationException("Generate should not run for this test.");
            }
        });
        return new ReportsService(
            facilities,
            reports: null,
            submission: null,
            validation: null,
            measure: null,
            Options.Create(new LinkUiFeatureOptions { AutomationEnabled = automationEnabled }),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<ReportsService>.Instance);
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private class Stub : DispatchProxy
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
            var created = DispatchProxy.Create<T, Stub>();
            ((Stub)(object)created).Methods = methods;
            return created;
        }
    }
}
