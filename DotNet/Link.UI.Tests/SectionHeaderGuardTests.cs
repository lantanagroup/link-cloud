using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class SectionHeaderGuardTests
{
    [Fact]
    public void Legends_do_not_sit_on_a_card_border()
    {
        var hits = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root(), "Views"), "*.cshtml", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Contains("<legend", StringComparison.Ordinal)
                    && (line.Contains("float-none", StringComparison.Ordinal) || line.Contains("w-auto", StringComparison.Ordinal)))
                    hits.Add(Rel(file) + ":" + (i + 1) + " legend sits on the border");
                if (line.Contains("<fieldset", StringComparison.Ordinal) && line.Contains("border", StringComparison.Ordinal))
                    hits.Add(Rel(file) + ":" + (i + 1) + " bordered fieldset");
            }
        }

        hits.Should().BeEmpty();
        var css = File.ReadAllText(Path.Combine(Root(), "wwwroot", "css", "site.css"));
        var rule = css.IndexOf("fieldset > legend", StringComparison.Ordinal);
        rule.Should().BeGreaterThan(-1);
        var body = css[rule..css.IndexOf('}', rule)];
        body.Should().Contain("float: none");
        body.Should().Contain("width: 100%");
        body.Should().NotContain("margin-top: -");

        File.ReadAllText(Path.Combine(Root(), "Views", "Operations", "_ProduceMessage.cshtml"))
            .Should().Contain("<h2 class=\"card-header h5 mb-0\">Produce a message</h2>");
    }

    [Fact]
    public void Disabled_buttons_keep_their_role_colour()
    {
        var css = File.ReadAllText(Path.Combine(Root(), "wwwroot", "css", "site.css"));
        var start = css.LastIndexOf("button.btn-success:disabled", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);
        var block = css[start..];
        block.Should().Contain("background-color: #000");
        block.Should().Contain("background-color: var(--au-success)");
        block.Should().Contain("background-color: var(--au-warning)");
        block.Should().Contain("background-color: var(--au-link)");
        block.Should().Contain("opacity: .82");
        block.Should().NotContain("#6c757d");
        block.Should().NotContain("#545c64");
    }

    [Fact]
    public void Raw_message_sections_share_the_dark_code_style()
    {
        var css = File.ReadAllText(Path.Combine(Root(), "wwwroot", "css", "site.css"));
        css.Should().Contain(".lu-kafka .lu-msg-raw");
        css.Should().Contain(".lu-kafka .lu-msg-raw pre.lu-json");
        var start = css.IndexOf(".lu-kafka .lu-msg-raw {", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);
        var body = css[start..css.IndexOf('}', start)];
        body.Should().Contain("background: var(--au-dark)");
        body.Should().Contain("color: #f4f4f4");
    }

    private static string Rel(string path) => Path.GetRelativePath(Root(), path);

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, "DotNet", "Link.UI");
    }
}
