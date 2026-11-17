using System.Reflection;
using FluentAssertions;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Link.UI.Tests;

/// <summary>
/// A soft-deleted report can be restored only when it had not started (Scheduled) or had
/// finished submission (Submitted). The UI disables Restore with a reason and the service refuses too.
/// </summary>
public class ReportRestoreRuleTests
{
    private const string FacilityId = "facility-1";
    private static readonly Guid ReportId = Guid.Parse("0f4bb0b5-3c1b-4f62-9f38-2d6d1b1b7a11");

    [Theory]
    [InlineData(ScheduleStatus.New, false)]
    [InlineData(ScheduleStatus.Scheduled, true)]
    [InlineData(ScheduleStatus.EndOfPeriod, false)]
    [InlineData(ScheduleStatus.Submitted, true)]
    [InlineData(ScheduleStatus.CompletedNotSubmitted, false)]
    public void Deleted_report_restores_only_when_scheduled_or_submitted(ScheduleStatus status, bool expected)
    {
        FacilityViewRules.CanRestore(status, deleted: true).Should().Be(expected);
        FacilityViewRules.CanRestore(status, deleted: true).Should().Be(status.CanRestore());
        FacilityViewRules.CanRestore(status, deleted: false).Should().BeFalse();
    }

    [Theory]
    [InlineData(ScheduleStatus.New, false)]
    [InlineData(ScheduleStatus.Scheduled, true)]
    [InlineData(ScheduleStatus.EndOfPeriod, false)]
    [InlineData(ScheduleStatus.Submitted, true)]
    [InlineData(ScheduleStatus.CompletedNotSubmitted, false)]
    public void Deleted_rows_carry_the_restore_rule(ScheduleStatus status, bool expected)
    {
        var row = FacilityViewService.ToReportRow(Schedule(status, deleted: true), census: null, population: null);

        row.Deleted.Should().BeTrue();
        row.CanRestore.Should().Be(expected);
    }

    [Theory]
    [InlineData(ScheduleStatus.New, false)]
    [InlineData(ScheduleStatus.Scheduled, true)]
    [InlineData(ScheduleStatus.EndOfPeriod, false)]
    [InlineData(ScheduleStatus.Submitted, true)]
    [InlineData(ScheduleStatus.CompletedNotSubmitted, false)]
    public async Task Restore_calls_admin_only_for_a_restorable_status(ScheduleStatus status, bool expected)
    {
        var restored = 0;
        var service = View(status, () => restored++);

        var result = await service.RestoreReportAsync(FacilityId, ReportId.ToString(), CancellationToken.None);

        result.Succeeded.Should().Be(expected);
        restored.Should().Be(expected ? 1 : 0);
        if (!expected)
            result.Message.Should().Be(ScheduleStatusExtensions.RestoreBlockedDetail);
    }

    [Theory]
    [InlineData(ScheduleStatus.Submitted, "can be restored later")]
    [InlineData(ScheduleStatus.Scheduled, "can be restored later")]
    [InlineData(ScheduleStatus.CompletedNotSubmitted, "Reports deleted mid flight cannot be restored.")]
    [InlineData(ScheduleStatus.New, "Reports deleted mid flight cannot be restored.")]
    [InlineData(ScheduleStatus.EndOfPeriod, "Reports deleted mid flight cannot be restored.")]
    public void Delete_and_abort_confirmations_say_whether_restore_will_work(ScheduleStatus status, string expected)
    {
        FacilityViewRules.RestoreNote(status).Should().Contain(expected);
    }

    [Fact]
    public void Blocked_restore_is_disabled_with_a_focusable_reason()
    {
        foreach (var relative in new[]
        {
            "DotNet/Link.UI/Views/Reports/Index.cshtml",
            "DotNet/Link.UI/Views/Tenants/_ViewReports.cshtml"
        })
        {
            var text = File.ReadAllText(RepoFile(relative));
            text.Should().Contain("@if (report.Deleted && report.CanRestore)");
            text.Should().Contain("else if (report.Deleted)");
            text.Should().Contain("<span class=\"d-inline-block\" tabindex=\"0\" data-bs-toggle=\"tooltip\" title=\"@FacilityViewRules.RestoreBlockedReason\"");
            text.Should().Contain("data-restore-blocked><button type=\"button\" class=\"btn btn-sm btn-success lu-icon-btn\" disabled");
            text.Should().NotContain("The report is soft-deleted and can be restored later.");
        }

        // The status list lives in Shared only.
        var rules = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/FacilityViewRules.cs"));
        rules.Should().Contain("status.CanRestore()");
        rules.Should().NotContain("ScheduleStatus.Scheduled or ScheduleStatus.Submitted");
    }

    private static ReportScheduleApiModel Schedule(ScheduleStatus status, bool deleted) => new()
    {
        Id = ReportId,
        FacilityId = FacilityId,
        Status = status,
        IsDeleted = deleted
    };

    private static FacilityViewService View(ScheduleStatus status, Action restored)
    {
        var reports = Stub.Create<IReportServiceClient>(new Dictionary<string, Func<object?[]?, object?>>(StringComparer.Ordinal)
        {
            ["GetScheduleAsync"] = _ => Task.FromResult(new LinkApiResponse<ReportScheduleApiModel>
            {
                StatusCode = StatusCodes.Status200OK,
                Body = Schedule(status, deleted: true)
            })
        });
        var admin = Stub.Create<IAdminBffIntegrationClient>(new Dictionary<string, Func<object?[]?, object?>>(StringComparer.Ordinal)
        {
            ["RestoreAggregateReportAsync"] = _ =>
            {
                restored();
                return Task.FromResult(new LinkApiResponse { StatusCode = StatusCodes.Status204NoContent });
            }
        });
        var facilities = Stub.Create<IFacilityServiceClient>(new Dictionary<string, Func<object?[]?, object?>>(StringComparer.Ordinal));
        return new FacilityViewService(
            facilities,
            reports,
            acquisition: null,
            normalization: null,
            dmrp: null,
            admin: admin,
            Options.Create(new LinkUiFeatureOptions()),
            NullLogger<FacilityViewService>.Instance);
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
