using System.Reflection;
using FluentAssertions;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Integration.Tenant;
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
    public async Task Resubmit_refuses_an_automation_facility_without_calling_tenant()
    {
        var regenerated = 0;
        var lookups = 0;
        var service = View(automationEnabled: true, owned: true, () => regenerated++, () => lookups++);

        var result = await service.ResubmitAsync(FacilityId, ReportId.ToString(), bypassSubmission: true, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Be(AutomationMarkRules.AdHocReportBlocked);
        regenerated.Should().Be(0);
        lookups.Should().Be(1);
    }

    [Fact]
    public async Task Resubmit_still_calls_tenant_for_a_facility_the_run_does_not_own()
    {
        var regenerated = 0;
        var service = View(automationEnabled: true, owned: false, () => regenerated++, () => { });

        var result = await service.ResubmitAsync(FacilityId, ReportId.ToString(), bypassSubmission: false, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Message.Should().Be("Report resubmitted.");
        regenerated.Should().Be(1);
    }

    [Fact]
    public async Task Resubmit_still_reaches_tenant_when_automation_is_off()
    {
        var regenerated = 0;
        var lookups = 0;
        var service = View(automationEnabled: false, owned: true, () => regenerated++, () => lookups++);

        var result = await service.ResubmitAsync(FacilityId, ReportId.ToString(), bypassSubmission: false, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        regenerated.Should().Be(1);
        lookups.Should().Be(0);
    }

    [Fact]
    public async Task Ad_hoc_generate_refuses_an_automation_facility_before_tenant()
    {
        var tenantCalls = 0;
        var lookups = 0;
        var service = Reports(automationEnabled: true, owned: true, () => tenantCalls++, () => lookups++);

        var page = await service.GenerateAsync(new GenerateReportInput { FacilityId = FacilityId }, CancellationToken.None);

        page.Error.Should().Be(AutomationMarkRules.AdHocReportBlocked);
        page.GeneratedReportId.Should().BeNull();
        page.AutomationFacilitiesExcluded.Should().BeTrue();
        tenantCalls.Should().Be(0);
        lookups.Should().Be(1);
    }

    [Fact]
    public async Task Ad_hoc_generate_keeps_going_for_a_facility_the_run_does_not_own()
    {
        var lookups = 0;
        var service = Reports(automationEnabled: true, owned: false, () => { }, () => lookups++);

        var page = await service.GenerateAsync(new GenerateReportInput { FacilityId = FacilityId }, CancellationToken.None);

        page.Error.Should().Be("Choose at least one measure.");
        lookups.Should().Be(1);
    }

    [Fact]
    public async Task Ad_hoc_generate_skips_the_ownership_read_when_automation_is_off()
    {
        var lookups = 0;
        var service = Reports(automationEnabled: false, owned: true, () => { }, () => lookups++);

        var page = await service.GenerateAsync(new GenerateReportInput { FacilityId = FacilityId }, CancellationToken.None);

        page.Error.Should().Be("Choose at least one measure.");
        page.AutomationFacilitiesExcluded.Should().BeFalse();
        lookups.Should().Be(0);
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
        reports.Should().Contain("ownership.Contains(report.FacilityId)");

        var tenants = File.ReadAllText(RepoFile("DotNet/Link.UI/Controllers/TenantsController.cs"));
        tenants.Should().Contain("isAutomationOwned: true");

        var generate = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Reports/Generate.cshtml"));
        generate.Should().Contain("AutomationFacilitiesExcluded");
        generate.Should().Contain("Automation facilities cannot be used here.");
    }

    private static FacilityViewService View(bool automationEnabled, bool owned, Action regenerated, Action lookedUp)
    {
        var facilities = Stub.Create<IFacilityServiceClient>(new Dictionary<string, Func<object?[]?, object?>>(StringComparer.Ordinal)
        {
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
            NullLogger<FacilityViewService>.Instance,
            Ownership(owned, lookedUp));
    }

    private static ReportsService Reports(bool automationEnabled, bool owned, Action tenantCalled, Action lookedUp)
    {
        var facilities = Stub.Create<IFacilityServiceClient>(new Dictionary<string, Func<object?[]?, object?>>(StringComparer.Ordinal)
        {
            ["GetAsync"] = _ =>
            {
                tenantCalled();
                throw new InvalidOperationException("Tenant should not be called for an automation facility.");
            },
            ["GenerateAdhocReportAsync"] = _ =>
            {
                tenantCalled();
                throw new InvalidOperationException("Tenant should not be called for an automation facility.");
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
            NullLogger<ReportsService>.Instance,
            Ownership(owned, lookedUp));
    }

    private static Func<CancellationToken, Task<AutomationOwnershipIndex>> Ownership(bool owned, Action lookedUp) =>
        _ =>
        {
            lookedUp();
            if (!owned)
                return Task.FromResult(AutomationOwnershipIndex.Empty);

            var runId = "11111111-1111-1111-1111-111111111111";
            return Task.FromResult(new AutomationOwnershipIndex(
                [FacilityId],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [FacilityId] = runId }));
        };

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
