using FluentAssertions;
using LantanaGroup.Link.Sdk.ApiClient;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class VendorDeleteNoticeTests
{
    [Fact]
    public void A_409_opens_Vendor_In_Use_with_the_problem_detail()
    {
        var detail = "Vendor 'Epic' is in use by ABC123 (Mercy General, version 2026.1). Clear the vendor version on each facility.";
        var action = VendorDeleteNotice.From(Response(409, "{\"detail\":\"" + detail + "\"}"), version: false);

        action.Succeeded.Should().BeFalse();
        action.DialogTitle.Should().Be("Vendor In Use");
        action.Message.Should().Be(detail);
        action.Message.Should().NotContain("Tenant returned HTTP");
        action.Message.Should().NotContain("409");
    }

    [Fact]
    public void A_409_without_a_detail_uses_the_vendor_fallback()
    {
        var action = VendorDeleteNotice.From(Response(409, "not-json"), version: false);

        action.DialogTitle.Should().Be(VendorDeleteNotice.InUseTitle);
        action.Message.Should().Be(VendorDeleteNotice.VendorInUseFallback);
    }

    [Fact]
    public void A_409_on_a_version_uses_the_version_fallback()
    {
        var action = VendorDeleteNotice.From(Response(409, ""), version: true);

        action.DialogTitle.Should().Be(VendorDeleteNotice.InUseTitle);
        action.Message.Should().Be(VendorDeleteNotice.VersionInUseFallback);
    }

    [Fact]
    public void A_long_409_detail_is_kept_whole()
    {
        var detail = "Version 'default' of vendor 'Epic' is in use. " + new string('x', 400);
        var action = VendorDeleteNotice.From(Response(409, "{\"detail\":\"" + detail + "\"}"), version: true);

        action.Message.Should().Be(detail);
        action.Message.Length.Should().BeGreaterThan(300);
    }

    [Fact]
    public void Another_failure_is_Delete_Failed_and_keeps_a_trace_id_once()
    {
        var action = VendorDeleteNotice.From(new LinkApiResponse
        {
            StatusCode = 500,
            RawBody = "{\"detail\":\"The vendor could not be deleted.\",\"traceId\":\"trace-1808\"}",
            TraceId = "trace-1808"
        }, version: false);

        action.DialogTitle.Should().Be(VendorDeleteNotice.FailedTitle);
        action.Message.Should().Be("The vendor could not be deleted. Trace id: trace-1808");
        action.Message.Split("trace-1808").Should().HaveCount(2);
    }

    [Fact]
    public void An_unreachable_tenant_is_named_and_a_version_failure_uses_its_own_fallback()
    {
        VendorDeleteNotice.From(Response(0, null), version: false).Message.Should().Be("Tenant could not be reached.");
        VendorDeleteNotice.From(Response(500, null), version: true).Message.Should().Be(VendorDeleteNotice.VersionFailedFallback);
    }

    [Fact]
    public void Both_vendor_deletes_use_the_notice_and_the_dialog_waits_for_the_page_swap()
    {
        var service = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/ConfigurationService.cs"));
        service.Should().Contain("VendorDeleteNotice.From(response, version: false)");
        service.Should().Contain("VendorDeleteNotice.From(response, version: true)");

        var alerts = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Configuration/_Alerts.cshtml"));
        alerts.Should().Contain("id=\"actionDialog\"");
        alerts.Should().Contain("actionDialogTitle");
        alerts.Should().Contain("document.readyState === \"loading\"");
        alerts.Should().Contain("au-refreshed");
        alerts.Should().NotContain("readyState !== \"loading\"");
    }

    private static LinkApiResponse Response(int status, string? body) => new() { StatusCode = status, RawBody = body };

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "link-cloud.sln")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
