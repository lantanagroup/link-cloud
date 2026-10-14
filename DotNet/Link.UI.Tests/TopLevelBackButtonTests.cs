using System.Text.RegularExpressions;
using FluentAssertions;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class TopLevelBackButtonTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/Home")]
    [InlineData("/Operations")]
    [InlineData("/Operations/Kafka")]
    [InlineData("/Tenants")]
    [InlineData("/Reports/Index")]
    [InlineData("/Configuration")]
    [InlineData("/Logs")]
    [InlineData("/Automation")]
    [InlineData("/System")]
    public void Sidebar_landing_pages_are_top_level(string path)
    {
        BackLinkRules.IsTopLevel(path).Should().BeTrue();
    }

    [Theory]
    [InlineData("/Logs/Sftp")]
    [InlineData("/Tenants/Report/abc")]
    [InlineData("/System/Themes")]
    public void Detail_pages_are_not_top_level(string path)
    {
        BackLinkRules.IsTopLevel(path).Should().BeFalse();
    }

    [Fact]
    public void The_shared_back_button_never_renders_on_a_top_level_page()
    {
        File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Shared/_BackButton.cshtml"))
            .Should().Contain("!BackLinkRules.IsTopLevel(current) &&");
    }

    [Fact]
    public void Every_sidebar_entry_opens_a_top_level_page()
    {
        var layout = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Shared/_Layout.cshtml"));
        var links = Regex.Matches(layout, "class=\"lu-nav-link[^\"]*\"[^>]*asp-controller=\"(\\w+)\" asp-action=\"(\\w+)\"");
        links.Should().NotBeEmpty();
        foreach (Match link in links.Where(l => l.Groups[1].Value != "Auth"))
        {
            var path = "/" + link.Groups[1].Value + (link.Groups[2].Value == "Index" ? "" : "/" + link.Groups[2].Value);
            BackLinkRules.IsTopLevel(path).Should().BeTrue(path);
        }
    }

    [Fact]
    public void Operations_opens_the_Kafka_console_directly()
    {
        File.Exists(RepoFile("DotNet/Link.UI/Views/Operations/Index.cshtml")).Should().BeFalse();
        File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Shared/_Layout.cshtml"))
            .Should().Contain("asp-controller=\"Operations\" asp-action=\"Kafka\"");
        File.ReadAllText(RepoFile("DotNet/Link.UI/Controllers/OperationsController.cs"))
            .Should().Contain("RedirectToAction(nameof(Kafka))");
    }

    [Fact]
    public void The_automation_tab_bar_shows_only_on_the_section_tab_pages()
    {
        var nav = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Automation/_Nav.cshtml"));
        nav.Should().Contain("@if (tabPage)");
        nav.Should().Contain("action.Equals(\"Index\"");
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "link-cloud.sln")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
