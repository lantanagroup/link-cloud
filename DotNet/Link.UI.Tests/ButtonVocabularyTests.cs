using FluentAssertions;
using Link.UI.Services;
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
        string[] allowed =
        [
            "Resubmit", "Cancel", "Abort", "Close", "Clear", "Reject",
            "Skip", "Undo", "Start over", "Recover", "Regenerate", "Stop"
        ];
        var hits = new List<string>();
        foreach (var file in SourceFiles())
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("btn-warning", StringComparison.Ordinal))
                    continue;
                var window = string.Join('\n', lines.Skip(i).Take(8));
                if (allowed.Any(word => window.Contains(word, StringComparison.OrdinalIgnoreCase)))
                    continue;
                hits.Add(Path.GetRelativePath(ProjectRoot(), file) + ":" + (i + 1));
            }
        }

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Section_tabs_are_not_action_buttons()
    {
        var css = File.ReadAllText(Path.Combine(ProjectRoot(), "wwwroot", "css", "site.css"));
        css.Should().Contain(".lu-section-nav a.lu-nav-current");
        css.Should().Contain(".btn-au-neutral");
        css.Should().NotContain(".lu-section-nav a.btn-au-action");
    }

    [Fact]
    public void Buttons_are_solid_and_the_green_matches_the_shared_token()
    {
        var css = File.ReadAllText(Path.Combine(ProjectRoot(), "wwwroot", "css", "site.css"));
        css.Should().Contain("--au-success:     #28a745;");
        css.Should().Contain(".btn-au-neutral");
        css.Should().Contain(".btn-danger:hover");

        var outline = SourceFiles()
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, index))
                .Where(row => row.line.Contains("btn-outline-", StringComparison.Ordinal))
                .Select(row => Path.GetRelativePath(ProjectRoot(), row.file) + ":" + (row.index + 1)))
            .ToList();
        outline.Should().BeEmpty();

        var linkButtons = SourceFiles()
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, index))
                .Where(row => row.line.Contains("btn-link", StringComparison.Ordinal)
                    && !row.line.Contains("patient-sort-button", StringComparison.Ordinal))
                .Select(row => Path.GetRelativePath(ProjectRoot(), row.file) + ":" + (row.index + 1)))
            .ToList();
        linkButtons.Should().BeEmpty();
    }

    [Fact]
    public void Status_pills_are_not_blue()
    {
        StatusPills.ForSchedule(LantanaGroup.Link.Shared.Application.Enums.ScheduleStatus.New)
            .Should().Be("au-badge-muted");
        StatusPills.ForSchedule(LantanaGroup.Link.Shared.Application.Enums.ScheduleStatus.Submitted)
            .Should().Be("au-badge-success");
        StatusPills.ForRun("Running").Should().Be("au-badge-active");

        var css = File.ReadAllText(Path.Combine(ProjectRoot(), "wwwroot", "css", "site.css"));
        var active = css.IndexOf(".au-badge-active", StringComparison.Ordinal);
        active.Should().BeGreaterThan(-1);
        var rule = css.Substring(active, Math.Min(180, css.Length - active));
        rule.Should().NotContain("--au-accent");
        rule.Should().NotContain("#4da3ff");
        rule.Should().NotContain("#0d6efd");
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
