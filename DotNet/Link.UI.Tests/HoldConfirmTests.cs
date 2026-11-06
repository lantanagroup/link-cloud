using FluentAssertions;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class HoldConfirmTests
{
    private static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "Link.UI", "Views")))
            dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir!, "Link.UI");
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));

    [Fact]
    public void Spec_serializes_action_details_risks_and_confirm()
    {
        var json = Hold.Of("Produce a message", "Produce one new message to T.", new[] { Hold.Pair("Topic", "T") }, new[] { new[] { "reason", "Reason" } }, new[] { "Cannot be recalled." }, "T", "Hold to produce");
        json.Should().Contain("\"title\":\"Produce a message\"").And.Contain("\"details\":[[\"Topic\",\"T\"]]").And.Contain("\"fields\":[[\"reason\",\"Reason\"]]").And.Contain("\"risks\":[\"Cannot be recalled.\"]").And.Contain("\"confirm\":\"T\"").And.Contain("\"label\":\"Hold to produce\"");
        Hold.DurationMs.Should().Be(1000);
    }

    [Fact]
    public void Modal_is_shared_from_the_layout_and_says_are_you_sure()
    {
        var layout = Read("Views", "Shared", "_Layout.cshtml");
        layout.Should().Contain("<partial name=\"_HoldConfirmModal\" />").And.Contain("~/js/hold-confirm.js");
        var modal = Read("Views", "Shared", "_HoldConfirmModal.cshtml");
        modal.Should().Contain("Are you sure?").And.Contain("id=\"lu-hold-risks\"").And.Contain("role=\"progressbar\"").And.Contain("aria-live=\"polite\"").And.Contain("data-hold-ms=\"@Hold.DurationMs\"");
    }

    [Fact]
    public void Hold_script_handles_pointer_keyboard_release_and_escape()
    {
        var js = Read("wwwroot", "js", "hold-confirm.js");
        foreach (var token in new[] { "pointerdown", "pointerup", "pointerleave", "pointercancel", "keydown", "keyup", "ev.repeat", "requestAnimationFrame", "aria-valuenow", "data-hold-target", "requestSubmit", "stopPropagation" })
            js.Should().Contain(token);
        js.Should().Contain("onDone");
        js.Should().Contain("ask:");
    }

    [Fact]
    public void Browser_confirm_is_gone_and_colliding_badges_are_distinct()
    {
        var hits = new List<string>();
        foreach (var folder in new[] { Path.Combine(Root(), "Views"), Path.Combine(Root(), "wwwroot", "js") })
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase) && !file.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (File.ReadAllText(file).Contains("confirm(", StringComparison.Ordinal))
                    hits.Add(Path.GetRelativePath(Root(), file));
            }
        }

        hits.Should().BeEmpty();
        var css = Read("wwwroot", "css", "site.css");
        css.Should().Contain(".au-badge-automation { background: #f7fee7");
        css.Should().Contain(".au-badge-attention { background: #fff1e8");
        css.Should().Contain(".au-badge-test { background: #fff1f2");
        Read("Views", "Shared", "_AutomationBadge.cshtml").Should().Contain("au-badge-automation").And.NotContain("au-badge-warning");
        Read("Views", "Tenants", "Facility.cshtml").Should().Contain("au-badge-test\">Test facility");
        StatusPills.ForKafkaHealth("Needs attention").Should().Be("au-badge-attention");
        StatusPills.ForKafkaHealth("ISR short").Should().Be("au-badge-warning");
        Read("wwwroot", "js", "facility-save.js").Should().Contain("luHoldConfirm.ask").And.Contain("beforeunload");
    }

    [Fact]
    public void Report_and_run_destructive_actions_hold_instead_of_a_browser_confirm()
    {
        foreach (var relative in new[] { new[] { "Views", "Reports", "Index.cshtml" }, new[] { "Views", "Tenants", "_ViewReports.cshtml" } })
        {
            var view = Read(relative);
            view.Should().Contain("data-hold=\"@Hold.Of(");
            view.Should().NotContain("onsubmit=\"return confirm(");
        }

        Read("Views", "Tenants", "Index.cshtml").Should().Contain("Hold to restore");
        Read("Views", "Tenants", "Facility.cshtml").Should().Contain("Hold to remove");

        var script = Read("Views", "Automation", "_RunDetailScript.cshtml");
        script.Should().Contain("luHoldConfirm.open");
        script.Should().NotContain("confirm('Delete this run");
        script.Should().NotContain("confirm('Cancel this running test");
    }

}
