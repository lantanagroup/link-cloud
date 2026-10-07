using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class ButtonVocabularyTests
{
    [Fact]
    public void Action_buttons_are_not_blue()
    {
        var hits = SourceFiles()
            .SelectMany(file => new[] { "btn-au-action", "btn-outline-primary", "btn-primary" }
                .Where(token => File.ReadAllText(file).Contains(token, StringComparison.Ordinal))
                .Select(token => Path.GetRelativePath(ProjectRoot(), file) + ": " + token))
            .ToList();

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Yellow_is_reserved_for_resubmit()
    {
        var hits = SourceFiles()
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, index))
                .Where(row => row.line.Contains("btn-warning", StringComparison.Ordinal)
                    && !row.line.Contains("Resubmit", StringComparison.Ordinal))
                .Select(row => Path.GetRelativePath(ProjectRoot(), row.file) + ":" + (row.index + 1)))
            .ToList();

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Section_tabs_are_not_action_buttons()
    {
        var css = File.ReadAllText(Path.Combine(ProjectRoot(), "wwwroot", "css", "site.css"));
        css.Should().Contain(".lu-section-nav a.lu-nav-current");
        css.Should().Contain(".btn-outline-danger:hover");
        css.Should().NotContain(".lu-section-nav a.btn-au-action");
    }

    private static IEnumerable<string> SourceFiles()
    {
        var root = ProjectRoot();
        return Directory.EnumerateFiles(Path.Combine(root, "Views"), "*.cshtml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "wwwroot", "js"), "*.js", SearchOption.AllDirectories));
    }

    private static string ProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, "DotNet", "Link.UI");
    }
}
