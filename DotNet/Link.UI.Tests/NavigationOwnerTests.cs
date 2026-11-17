using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class NavigationOwnerTests
{
    [Fact]
    public void The_sidebar_highlights_the_owning_section_not_the_controller()
    {
        var layout = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Shared/_Layout.cshtml"));
        layout.Should().Contain("ViewData[\"NavSection\"]");
        layout.Should().NotMatchRegex("lu-nav-link @\\(Ctrl\\(");
        layout.Should().Contain("@(automationActive ? \"active\" : \"\")");
    }

    [Fact]
    public void An_automation_report_belongs_to_automation_and_goes_back_to_its_origin()
    {
        var controller = File.ReadAllText(RepoFile("DotNet/Link.UI/Controllers/AutomationController.cs"));
        controller.Should().Contain("returnUrl = origin");
        var report = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Tenants/Report.cshtml"));
        report.Should().Contain("ViewData[\"NavSection\"] = \"Automation\";");
    }

    [Fact]
    public void Every_back_link_uses_the_shared_back_button_at_the_top_left()
    {
        var root = RepoFile("DotNet/Link.UI/Views");
        var bad = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*.cshtml", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) == "_BackButton.cshtml")
                continue;
            var text = File.ReadAllText(file);
            foreach (Match link in Regex.Matches(text, "<a\\b[^>]*>\\s*(<i[^>]*></i>)?\\s*Back\\b"))
                bad.Add(Path.GetFileName(file) + ": " + link.Value);
        }

        bad.Should().BeEmpty();
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "link-cloud.sln")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}