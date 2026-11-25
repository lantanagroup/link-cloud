using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class DismissMarkTests
{
    [Fact]
    public void Notices_close_with_a_colour_matched_mark()
    {
        var root = Root();
        var views = Directory.GetFiles(Path.Combine(root, "Views"), "*.cshtml", SearchOption.AllDirectories);
        var oldDismiss = views.Where(path => File.ReadAllText(path).Contains("Dismiss result", StringComparison.Ordinal)).ToList();
        oldDismiss.Should().BeEmpty();
        var solidDismiss = views.Where(path => File.ReadAllText(path).Contains("btn-warning lu-result-dismiss", StringComparison.Ordinal)).ToList();
        solidDismiss.Should().BeEmpty();

        foreach (var name in new[] { "System\\_Alerts.cshtml", "Configuration\\_Alerts.cshtml", "Logs\\_Alerts.cshtml" })
        {
            var text = File.ReadAllText(Path.Combine(root, "Views", name));
            text.Should().Contain("class=\"lu-dismiss\" aria-label=\"Dismiss\"");
            text.Should().Contain("&times;");
        }

        var css = File.ReadAllText(Path.Combine(root, "wwwroot", "css", "site.css"));
        css.Should().Contain(".lu-dismiss");
        css.Should().Contain("color: currentColor;");
        css.Should().Contain(".alert.lu-alert > .lu-dismiss");
        css.Should().Contain(".alert > .btn-close:empty::before");

        var script = File.ReadAllText(Path.Combine(root, "wwwroot", "js", "live-region.js"));
        script.Should().Contain(".alert-success, .alert-danger, .alert-warning");
        script.Should().Contain("aria-label\", \"Dismiss\"");
        script.Should().Contain(".modal, .toast, .offcanvas");
        script.Should().Contain("wireDismiss(document)");
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Link.UI", "Link.UI.csproj");
            if (File.Exists(candidate))
                return Path.Combine(dir.FullName, "Link.UI");
            var scratch = Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj");
            if (File.Exists(scratch))
                return Path.Combine(dir.FullName, "DotNet", "Link.UI");
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Link.UI project was not found.");
    }
}
