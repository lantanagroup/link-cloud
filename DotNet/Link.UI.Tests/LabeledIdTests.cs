using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class LabeledIdTests
{
    [Fact]
    public void Copy_control_is_one_neutral_icon_button()
    {
        var button = File.ReadAllText(Repo("Views/Shared/_CopyButton.cshtml"));
        button.Should().Contain("bi-copy");
        button.Should().Contain("btn-au-link");
        button.Should().NotContain("btn-au-neutral");
        button.Should().Contain("data-lu-copy");
        button.Should().Contain("aria-label");
        button.Should().Contain(">Copy</span>");

        var script = File.ReadAllText(Repo("wwwroot/js/live-region.js"));
        script.Should().Contain("bi-check2");
        script.Should().Contain("bi-copy");
        script.Should().Contain("Copied");

        var textButtons = Directory.EnumerateFiles(Path.Combine(Root(), "Views"), "*.cshtml", SearchOption.AllDirectories)
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, index))
                .Where(row => row.line.Contains(">Copy</button>", StringComparison.Ordinal))
                .Select(row => Path.GetRelativePath(Root(), row.file) + ":" + (row.index + 1)))
            .ToList();
        textButtons.Should().BeEmpty();
    }

    [Fact]
    public void Report_row_actions_are_spaced_icon_buttons()
    {
        foreach (var relative in new[] { "Views/Reports/Index.cshtml", "Views/Tenants/_ViewReports.cshtml" })
        {
            var text = File.ReadAllText(Repo(relative));
            text.Should().Contain("lu-row-actions");
            text.Should().Contain("bi-arrow-repeat");
            text.Should().Contain("title=\"Resubmit\"");
            text.Should().Contain("aria-label=\"Resubmit\"");
            text.Should().Contain("bi-trash");
            text.Should().Contain("aria-label=\"Clean up\"");
            text.Should().Contain("return confirm(");
            text.Should().Contain("id=\"resubmitDialog\"");
        }

        var css = File.ReadAllText(Repo("wwwroot/css/site.css"));
        css.Should().Contain(".lu-row-actions");
        css.Should().Contain("flex-wrap: nowrap");
        css.Should().Contain("flex-wrap: wrap");
    }

    [Fact]
    public void Report_pages_label_facility_and_report_ids()
    {
        var identity = File.ReadAllText(Repo("Views/Shared/_ReportIdentity.cshtml"));
        identity.Should().Contain("Label = \"Facility\"");
        identity.Should().Contain("Label = \"Report ID\"");
        identity.Should().Contain("Copy facility id");
        identity.Should().Contain("Copy report id");

        foreach (var relative in new[]
        {
            "Views/Tenants/Report.cshtml",
            "Views/Reports/Validation.cshtml",
            "Views/Reports/Acquisition.cshtml",
            "Views/Reports/Measure.cshtml"
        })
        {
            File.ReadAllText(Repo(relative)).Should().Contain("_ReportIdentity");
        }
    }

    private static string Repo(string relative) => Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar));

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, "DotNet", "Link.UI");
    }
}
