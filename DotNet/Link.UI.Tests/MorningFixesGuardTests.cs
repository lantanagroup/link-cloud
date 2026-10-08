using System.Text.RegularExpressions;
using FluentAssertions;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

/// <summary>
/// App-wide checks for the shell patterns: sign-in links, cohort columns, secret inputs,
/// overnight windows, section errors, the success token, plain values, copy buttons, and row actions.
/// </summary>
public class MorningFixesGuardTests
{
    [Fact]
    public void Scenario_editor_has_no_cohort_scenario_selector()
    {
        var editor = File.ReadAllText(Path.Combine(Root(), "Views", "Shared", "_ScenarioEditorModal.cshtml"));
        editor.Should().NotContain("buildScenarioSelect");
        editor.Should().NotContain("clinicalScenarioCatalog");
        editor.Should().NotContain("cohort-scenario");
        editor.Should().Contain("Patient configuration");
        editor.Should().Contain("Predicted");
        editor.Should().Contain("eligibleClinicalScenarioIds: ids.length ? [ids[0]] : []");
        editor.Should().NotContain("function sid(");
    }

    [Fact]
    public void Section_errors_use_the_shared_helper()
    {
        File.ReadAllText(Path.Combine(Root(), "Views", "Shared", "_Layout.cshtml"))
            .Should().Contain("section-validity.js");
        var css = File.ReadAllText(Path.Combine(Root(), "wwwroot", "css", "site.css"));
        css.Should().Contain(".accordion-button.lu-section-invalid");
        css.Should().Contain("lu-section-warning");
        var rule = css[(css.IndexOf(".accordion-button.lu-section-invalid", StringComparison.Ordinal))..];
        rule[..Math.Min(rule.Length, 400)].Should().Contain("var(--au-danger)");
        File.ReadAllText(Path.Combine(Root(), "wwwroot", "js", "facility-save.js"))
            .Should().Contain("luSectionValidity.mark");

        var hits = ProductFiles("*.cshtml", "Views")
            .Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js")))
            .Where(file => !file.EndsWith("section-validity.js", StringComparison.OrdinalIgnoreCase))
            .Select(file => (file, text: File.ReadAllText(file)))
            .Where(row => row.text.Contains("classList.add('is-invalid')", StringComparison.Ordinal)
                || row.text.Contains("classList.add(\"is-invalid\")", StringComparison.Ordinal))
            .Where(row => !row.text.Contains("luSectionValidity", StringComparison.Ordinal))
            .Select(row => Rel(row.file))
            .ToList();
        hits.Should().BeEmpty();
    }

    [Fact]
    public void Success_green_is_only_the_shared_token()
    {
        var hits = new List<string>();
        foreach (var file in ProductFiles("*.css", Path.Combine("wwwroot", "css"))
            .Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js")))
            .Concat(ProductFiles("*.cshtml", "Views")))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("#28a745", StringComparison.OrdinalIgnoreCase)
                    && !lines[i].Contains("--au-success:", StringComparison.Ordinal))
                    hits.Add(Rel(file) + ":" + (i + 1));
                if (Regex.IsMatch(lines[i], @"rgb\(\s*40\s*,\s*167\s*,\s*69"))
                    hits.Add(Rel(file) + ":" + (i + 1) + " rgb");
            }
        }

        hits.Should().BeEmpty();
        var css = File.ReadAllText(Path.Combine(Root(), "wwwroot", "css", "site.css"));
        css.Should().Contain("--au-success:     #28a745;");
        css.Should().Contain("--au-success-rgb: 40, 167, 69;");
        File.ReadAllText(Path.Combine(Root(), "wwwroot", "js", "chart-palette.js"))
            .Should().Contain("getPropertyValue(\"--au-success\")");
        File.ReadAllText(Path.Combine(Root(), "wwwroot", "js", "kafka-ops.js"))
            .Should().Contain("getPropertyValue(\"--au-success\")");
    }

    [Fact]
    public void Plain_values_are_not_dark_pills()
    {
        var dark = new Regex(@"\b(bg-dark|bg-secondary|text-bg-dark|text-bg-secondary)\b", RegexOptions.IgnoreCase);
        var hits = new List<string>();
        foreach (var file in ProductFiles("*.cshtml", "Views").Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js"))))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("<span", StringComparison.OrdinalIgnoreCase) && !lines[i].Contains("class=\"badge", StringComparison.OrdinalIgnoreCase) && !lines[i].Contains("class='badge", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (dark.IsMatch(lines[i]))
                    hits.Add(Rel(file) + ":" + (i + 1));
            }
        }

        hits.Should().BeEmpty();
        var css = File.ReadAllText(Path.Combine(Root(), "wwwroot", "css", "site.css"));
        var active = css.IndexOf(".au-badge-success", StringComparison.Ordinal);
        css.Substring(active, 180).Should().Contain("--au-success-soft");
        css.Substring(active, 180).Should().NotContain("color: #fff");
    }

    [Fact]
    public void Copy_buttons_are_only_for_identifiers()
    {
        LabeledIdRules.AllowsCopy("Seed").Should().BeFalse();
        LabeledIdRules.AllowsCopy("Date").Should().BeFalse();
        LabeledIdRules.AllowsCopy("Time").Should().BeFalse();
        LabeledIdRules.AllowsCopy("Count").Should().BeFalse();
        LabeledIdRules.AllowsCopy("Name").Should().BeFalse();
        LabeledIdRules.AllowsCopy("Measures").Should().BeFalse();
        LabeledIdRules.AllowsCopy("Package").Should().BeFalse();
        LabeledIdRules.AllowsCopy("Facility").Should().BeTrue();
        LabeledIdRules.AllowsCopy("Report ID").Should().BeTrue();
        LabeledIdRules.AllowsCopy("Measure").Should().BeTrue();
        LabeledIdRules.AllowsCopy("File").Should().BeTrue();

        var copyPartials = ProductFiles("*.cshtml", "Views")
            .Where(file => File.ReadAllText(file).Contains("name=\"_CopyButton\"", StringComparison.Ordinal)
                && !file.EndsWith("_LabeledId.cshtml", StringComparison.OrdinalIgnoreCase))
            .Select(Rel)
            .ToList();
        copyPartials.Should().BeEmpty();

        var inline = ProductFiles("*.cshtml", "Views")
            .Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js")))
            .Where(file => File.ReadAllText(file).Contains("data-lu-copy", StringComparison.Ordinal))
            .Select(Rel)
            .Where(path => !path.EndsWith("_CopyButton.cshtml", StringComparison.OrdinalIgnoreCase)
                && !path.EndsWith("_Layout.cshtml", StringComparison.OrdinalIgnoreCase)
                && !path.EndsWith("live-region.js", StringComparison.OrdinalIgnoreCase))
            .ToList();
        inline.Should().BeEmpty();
        File.ReadAllText(Path.Combine(Root(), "Views", "Automation", "Scenarios.cshtml"))
            .Should().NotContain("Copy seed");
    }

    [Fact]
    public void Row_icon_actions_use_the_shared_class()
    {
        var css = File.ReadAllText(Path.Combine(Root(), "wwwroot", "css", "site.css"));
        css.Should().Contain(".btn.lu-icon-btn");
        css.Should().Contain(".btn.btn-danger.lu-icon-btn");
        var ruleAt = css.IndexOf(".btn.lu-icon-btn", StringComparison.Ordinal);
        css.Substring(ruleAt, 500).Should().Contain("background-color: #fff");

        var icon = new Regex(@"<i\s+class=""bi bi-(trash|pencil|eye|copy|download|x-lg|box-arrow-up-right)""", RegexOptions.IgnoreCase);
        var hits = new List<string>();
        foreach (var file in ProductFiles("*.cshtml", "Views").Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js"))))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!icon.IsMatch(lines[i]))
                    continue;
                var start = Math.Max(0, i - 6);
                var window = string.Join('\n', lines.Skip(start).Take(i - start + 3));
                if (!window.Contains("btn", StringComparison.Ordinal))
                    continue;
                if (window.Contains("lu-icon-btn", StringComparison.Ordinal) || window.Contains("lu-copy", StringComparison.Ordinal) || window.Contains("data-lu-copy", StringComparison.Ordinal))
                    continue;
                if (window.Contains("au-info-toggle", StringComparison.Ordinal) || window.Contains("lu-nav", StringComparison.Ordinal))
                    continue;
                hits.Add(Rel(file) + ":" + (i + 1));
            }
        }

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Secret_inputs_are_masked_and_do_not_echo()
    {
        var sensitive = new Regex(@"password|secret|token|apikey|credential", RegexOptions.IgnoreCase);
        var input = new Regex(@"<input\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var attr = new Regex(@"(\w[\w-]*)\s*=\s*(""[^""]*""|'[^']*')", RegexOptions.IgnoreCase);
        var label = new Regex(@"<label\b[^>]*for\s*=\s*(""[^""]+""|'[^']+')[^>]*>(.*?)</label>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var hits = new List<string>();
        foreach (var file in ProductFiles("*.cshtml", "Views").Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js"))))
        {
            var text = File.ReadAllText(file);
            var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in label.Matches(text))
            {
                var id = match.Groups[1].Value.Trim('"', '\'');
                var caption = Regex.Replace(match.Groups[2].Value, "<[^>]+>", " ");
                labels[id] = caption;
            }

            foreach (Match match in input.Matches(text))
            {
                var tag = match.Value;
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match piece in attr.Matches(tag))
                    values[piece.Groups[1].Value] = piece.Groups[2].Value.Trim('"', '\'');
                values.TryGetValue("type", out var type);
                if (type is "checkbox" or "radio" or "hidden" or "submit" or "button" or "file")
                    continue;
                values.TryGetValue("name", out var name);
                values.TryGetValue("id", out var id);
                values.TryGetValue("aria-label", out var aria);
                labels.TryGetValue(id ?? "", out var caption);
                var blob = string.Join(' ', new[] { name, id, aria, caption }.Where(part => !string.IsNullOrWhiteSpace(part)));
                if (!sensitive.IsMatch(blob))
                    continue;
                if ((name ?? "").EndsWith("TokenUrl", StringComparison.OrdinalIgnoreCase)
                    || (id ?? "").EndsWith("TokenUrl", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.Equals(type, "password", StringComparison.OrdinalIgnoreCase))
                    hits.Add(Rel(file) + " " + (name ?? id ?? "input") + " is not type=password");
                values.TryGetValue("autocomplete", out var autocomplete);
                if (autocomplete is not ("new-password" or "off"))
                    hits.Add(Rel(file) + " " + (name ?? id ?? "input") + " autocomplete");
                if (values.TryGetValue("value", out var value) && value.Length > 0 && value != "\"\"" && !string.Equals(value, "", StringComparison.Ordinal))
                    hits.Add(Rel(file) + " " + (name ?? id ?? "input") + " echoes a value");
            }
        }

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Overnight_hint_is_tied_to_min_after_max()
    {
        var editor = File.ReadAllText(Path.Combine(Root(), "Views", "Shared", "_FhirQueryEditor.cshtml"));
        editor.Should().Contain("id=\"pullWindowHint\"");
        editor.Should().Contain("OvernightWindowHint");
        editor.Should().Contain("minSeconds > maxSeconds");
        editor.Should().Contain("form-text text-muted");
        File.ReadAllText(Path.Combine(Root(), "Services", "FacilityAcquisitionRules.cs"))
            .Should().Contain("Overnight window (crosses midnight)");
    }

    private static IEnumerable<string> ProductFiles(string pattern, string relative)
    {
        var dir = Path.Combine(Root(), relative);
        return Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, "DotNet", "Link.UI");
    }

    private static string Rel(string path) => Path.GetRelativePath(Root(), path);
}
