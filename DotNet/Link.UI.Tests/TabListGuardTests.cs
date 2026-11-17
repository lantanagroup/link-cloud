using FluentAssertions;
using Xunit;
using System.Text.RegularExpressions;

namespace Link.UI.Tests;

public class TabListGuardTests
{
    // Bootstrap 5.1 Tab finds its list with closest('.nav, .list-group'); without one, show() throws "Cannot read properties of null (reading 'children')".
    [Fact]
    public void Every_bootstrap_tab_button_sits_in_a_nav_or_list_group_and_targets_a_pane_in_the_same_view()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Link.UI", "Views");
        var bad = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*.cshtml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (!text.Contains("data-bs-toggle=\"tab\"", StringComparison.Ordinal) && !text.Contains("data-bs-toggle=\"pill\"", StringComparison.Ordinal))
                continue;
            foreach (Match list in Regex.Matches(text, "<(ul|div|nav)[^>]*role=\"tablist\"[^>]*>"))
            {
                if (!Regex.IsMatch(list.Value, "class=\"[^\"]*\\b(nav|list-group)\\b"))
                    bad.Add(Path.GetFileName(file) + ": tablist without .nav: " + list.Value);
            }
            foreach (Match target in Regex.Matches(text, "data-bs-toggle=\"(?:tab|pill)\"[^>]*data-bs-target=\"#([^\"@]+)\""))
            {
                if (!text.Contains("id=\"" + target.Groups[1].Value + "\"", StringComparison.Ordinal))
                    bad.Add(Path.GetFileName(file) + ": missing pane #" + target.Groups[1].Value);
            }
        }

        bad.Should().BeEmpty();
    }
}

