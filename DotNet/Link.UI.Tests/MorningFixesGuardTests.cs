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
        // Kafka charts use one neutral grey, so they carry no success green at all.
        var kafkaScript = File.ReadAllText(Path.Combine(Root(), "wwwroot", "js", "kafka-ops.js"));
        kafkaScript.Should().Contain("#6c757d");
        kafkaScript.Should().NotContain("--au-success");
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
        LabeledIdRules.ShowsCopy("Facility", "11111111-1111-1111-1111-111111111111").Should().BeTrue();
        LabeledIdRules.ShowsCopy("Facility", "{11111111-1111-1111-1111-111111111111}").Should().BeTrue();
        LabeledIdRules.ShowsCopy("Facility", "not-a-guid").Should().BeFalse();
        LabeledIdRules.ShowsCopy("Facility", "2026-10-09").Should().BeFalse();
        LabeledIdRules.ShowsCopy("Name", "11111111-1111-1111-1111-111111111111").Should().BeFalse();
        LabeledIdRules.ShowsCopy("Seed", "11111111-1111-1111-1111-111111111111").Should().BeFalse();
        LabeledIdRules.ShowsCopy("Report ID", "  ").Should().BeFalse();
        LabeledIdRules.IsGuid(null).Should().BeFalse();

        File.ReadAllText(Path.Combine(Root(), "Views", "Shared", "_LabeledId.cshtml"))
            .Should().Contain("LabeledIdRules.ShowsCopy");
        File.ReadAllText(Path.Combine(Root(), "Views", "Shared", "_Layout.cshtml"))
            .Should().Contain("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

        var copyPartials = ProductFiles("*.cshtml", "Views")
            .Where(file => File.ReadAllText(file).Contains("name=\"_CopyButton\"", StringComparison.Ordinal)
                && !file.EndsWith("_LabeledId.cshtml", StringComparison.OrdinalIgnoreCase)
                && !file.EndsWith("_MessageDetail.cshtml", StringComparison.OrdinalIgnoreCase))
            .Select(Rel)
            .ToList();
        copyPartials.Should().BeEmpty();
        var messageDetail = File.ReadAllText(Path.Combine(Root(), "Views", "Operations", "_MessageDetail.cshtml"));
        messageDetail.Should().Contain("Copy headers");
        messageDetail.Should().Contain("Copy key");
        messageDetail.Should().Contain("Copy value");
        messageDetail.Should().Contain("Copy all");

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
                if (window.Contains("au-info-toggle", StringComparison.Ordinal) || window.Contains("lu-nav", StringComparison.Ordinal) || window.Contains("lu-header-tool", StringComparison.Ordinal))
                    continue;
                hits.Add(Rel(file) + ":" + (i + 1));
            }
        }

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Row_icon_actions_are_solid_fills()
    {
        var css = File.ReadAllText(Path.Combine(Root(), "wwwroot", "css", "site.css"));
        var start = css.IndexOf(".btn.lu-icon-btn,", StringComparison.Ordinal);
        var end = css.IndexOf("/* lu-row-action-fill-end */", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);
        end.Should().BeGreaterThan(start);
        var block = css[start..end];

        block.Should().NotContain("background-color: #fff");
        block.Should().NotContain("background: #fff");
        block.Should().NotContain("background-color: transparent");
        block.Should().NotContain("#6c757d");
        block.Should().NotContain("#0d6efd");
        block.Should().Contain("background-color: var(--au-link)");
        block.Should().Contain("background-color: #000");
        block.Should().Contain("background-color: var(--au-success)");
        block.Should().Contain("background-color: var(--au-warning)");
        block.Should().Contain("background-color: var(--au-danger)");
        block.Should().Contain("color: #fff");

        var danger = block.IndexOf(".btn.btn-danger.lu-icon-btn,", StringComparison.Ordinal);
        danger.Should().BeGreaterThan(-1);
        var dangerRule = block[danger..];
        dangerRule.Should().Contain("background-color: var(--au-danger)");
        dangerRule.Should().Contain("color: #fff");
        dangerRule.Should().NotContain("background-color: #fff");
    }

    [Fact]
    public void Action_buttons_are_not_outline_or_gray()
    {
        string[] forbidden = ["btn-outline-", "btn-au-neutral", "btn-au-close", "btn-secondary", "btn-light", "btn-dark"];
        var hits = new List<string>();
        foreach (var file in ProductFiles("*.cshtml", "Views").Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js"))))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (var token in forbidden)
                {
                    if (lines[i].Contains(token, StringComparison.Ordinal))
                        hits.Add(Rel(file) + ":" + (i + 1) + " " + token);
                }
            }
        }

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Icon_only_buttons_have_a_visible_label()
    {
        var hits = new List<string>();
        foreach (var file in ProductFiles("*.cshtml", "Views").Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js"))))
            hits.AddRange(IconOnlyButtons(Rel(file), File.ReadAllText(file)));

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Copy_buttons_are_not_attached_to_non_guid_values()
    {
        var attr = new Regex(@"data-lu-copy\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
        var hits = new List<string>();
        foreach (var file in ProductFiles("*.cshtml", "Views").Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js"))))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match match in attr.Matches(lines[i]))
                {
                    var value = match.Groups[1].Value;
                    if (value.Contains('@', StringComparison.Ordinal) || value.Contains('+', StringComparison.Ordinal))
                        continue;
                    if (!LabeledIdRules.IsGuid(value))
                        hits.Add(Rel(file) + ":" + (i + 1) + " " + value);
                }
            }
        }

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Kafka_operations_buttons_use_the_shared_colors()
    {
        var index = File.ReadAllText(Path.Combine(Root(), "Views", "Operations", "Index.cshtml"));
        index.Should().Contain("btn btn-au-link");
        index.Should().Contain("Open Kafka");

        var kafka = File.ReadAllText(Path.Combine(Root(), "Views", "Operations", "Kafka.cshtml"));
        kafka.Should().Contain("btn btn-au-execute\" type=\"submit\">Search");

        var overview = File.ReadAllText(Path.Combine(Root(), "Views", "Operations", "_Overview.cshtml"));
        overview.Should().Contain("bi-hdd-network");
        overview.Should().Contain(">Open</span>");
        overview.Should().Contain(">Migrate</a>");
        overview.Should().NotContain("btn-au-neutral");

        var topic = File.ReadAllText(Path.Combine(Root(), "Views", "Operations", "_Topic.cshtml"));
        topic.Should().Contain("btn-au-link");
        topic.Should().Contain("Migrate instead");
        topic.Should().Contain(">Preview increase</span>");

        var consumers = File.ReadAllText(Path.Combine(Root(), "Views", "Operations", "_Consumers.cshtml"));
        consumers.Should().Contain(">Open</span>");
        var brokers = File.ReadAllText(Path.Combine(Root(), "Views", "Operations", "_Brokers.cshtml"));
        brokers.Should().Contain(">Open</span>");
        brokers.Should().Contain(">Rebalance</span>");

        var migrate = File.ReadAllText(Path.Combine(Root(), "Views", "Operations", "_Migrate.cshtml"));
        migrate.Should().Contain("btn-au-execute\" type=\"submit\" formaction=\"/Operations/Kafka/migrations/plan\">Dry run");
        migrate.Should().Contain("btn-au-execute\" type=\"submit\" aria-label=\"Execute migration\">Execute");
        migrate.Should().Contain("btn-au-execute\" type=\"submit\">Go");
        migrate.Should().Contain("btn-au-execute\" type=\"submit\" name=\"action\" value=\"forward\">Continue forward");
        migrate.Should().Contain("btn-warning\" type=\"submit\" aria-label=\"Abort migration\">Abort");
        migrate.Should().Contain("btn-warning\" type=\"submit\" name=\"action\" value=\"original\">Recover original");
        migrate.Should().Contain("btn-danger\" type=\"submit\">Delete backup");
        migrate.Should().Contain("btn-danger\" type=\"submit\" name=\"action\" value=\"deleteForeign\">Delete empty foreign topic");

        var change = File.ReadAllText(Path.Combine(Root(), "Views", "Operations", "_ChangeRequest.cshtml"));
        change.Should().Contain(">Execute</span>");
        change.Should().Contain("btn-au-execute");
        change.Should().Contain(">Cancel</span>");
        change.Should().Contain("btn-warning");
    }

    [Fact]
    public void Integration_posts_and_the_health_check_execute_and_stop_warns()
    {
        var integration = File.ReadAllText(Path.Combine(Root(), "Views", "System", "Integration.cshtml"));
        integration.Should().Contain("btn btn-au-execute\">Start consumers");
        integration.Should().Contain("btn btn-au-execute\">Read consumers");
        integration.Should().Contain("btn btn-warning\">Stop consumers");
        integration.Should().Contain("btn btn-au-execute\">Post report scheduled");
        integration.Should().Contain("btn btn-au-execute\">Post patient list");
        integration.Should().Contain("btn btn-au-execute\">Post patient event");
        integration.Should().Contain("btn btn-au-execute\">Post data acquisition");
        integration.Should().Contain("btn btn-au-execute\">Post patient acquired");
        integration.Should().NotContain("btn btn-success\">Post");
        integration.Should().NotContain("btn btn-danger\">Stop");

        var health = File.ReadAllText(Path.Combine(Root(), "Views", "System", "Health.cshtml"));
        health.Should().Contain("btn btn-au-execute\">Check");
    }

    [Fact]
    public void Execute_buttons_are_not_green()
    {
        var verb = new Regex(@"\b(Search|Submit|Request|Generate|Run|Execute|Check|Start|Post|Analyze|Evaluate|Send|Filter|Upload|Confirm step|Look up|Suggest)\b");
        var button = new Regex(@"<(button|label)\b[^>]*\bbtn-success\b[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var hits = new List<string>();
        foreach (var file in ProductFiles("*.cshtml", "Views").Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js"))))
        {
            var text = File.ReadAllText(file);
            foreach (Match match in button.Matches(text))
            {
                var plain = Regex.Replace(match.Value, "<[^>]+>", " ");
                plain = Regex.Replace(plain, @"\s+", " ").Trim();
                if (verb.IsMatch(plain))
                    hits.Add(Rel(file) + " " + plain);
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

    private static IEnumerable<string> IconOnlyButtons(string relative, string text)
    {
        var hits = new List<string>();
        var i = 0;
        while (i < text.Length)
        {
            var start = text.IndexOf('<', i);
            if (start < 0)
                break;
            var name = TagName(text, start);
            if (name is not ("button" or "a" or "label"))
            {
                i = start + 1;
                continue;
            }

            if (!TryReadElement(text, start, name, out var openEnd, out var closeStart, out var closeEnd))
            {
                i = start + 1;
                continue;
            }

            var opening = text[start..openEnd];
            if (opening.Contains("btn", StringComparison.Ordinal)
                && !SkippedControl(opening))
            {
                var body = text[openEnd..closeStart];
                if ((body.Contains("<i", StringComparison.OrdinalIgnoreCase) || body.Contains("bi bi-", StringComparison.Ordinal))
                    && !HasVisibleLabel(body))
                {
                    var line = text[..start].Count(ch => ch == '\n') + 1;
                    hits.Add(relative + ":" + line);
                }
            }

            i = closeEnd;
        }

        return hits;
    }

    private static bool SkippedControl(string opening) =>
        opening.Contains("accordion-button", StringComparison.Ordinal)
        || opening.Contains("btn-close", StringComparison.Ordinal)
        || opening.Contains("dropdown-item", StringComparison.Ordinal)
        || opening.Contains("list-group-item", StringComparison.Ordinal)
        || opening.Contains("patient-sort-button", StringComparison.Ordinal)
        || opening.Contains("lu-nav-toggle", StringComparison.Ordinal)
        || opening.Contains("au-info-toggle", StringComparison.Ordinal)
        || opening.Contains("lu-copy", StringComparison.Ordinal)
        || (opening.Contains("dropdown-toggle", StringComparison.Ordinal)
            && !opening.Contains("pc-picker-toggle", StringComparison.Ordinal));

    private static bool HasVisibleLabel(string body)
    {
        var shown = Regex.Replace(body, @"<span\b[^>]*\bvisually-hidden\b[^>]*>.*?</span>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        shown = Regex.Replace(shown, @"<i\b[^>]*>.*?</i>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        shown = Regex.Replace(shown, "<[^>]+>", " ");
        return Regex.IsMatch(shown, @"[A-Za-z0-9]");
    }

    private static string? TagName(string text, int start)
    {
        if (start + 1 >= text.Length || text[start] != '<')
            return null;
        var i = start + 1;
        if (i < text.Length && text[i] == '/')
            return null;
        var begin = i;
        while (i < text.Length && char.IsLetter(text[i]))
            i++;
        if (i == begin)
            return null;
        return text[begin..i].ToLowerInvariant();
    }

    private static bool TryReadElement(string text, int start, string name, out int openEnd, out int closeStart, out int closeEnd)
    {
        openEnd = closeStart = closeEnd = 0;
        var j = start + 1;
        char? quote = null;
        var depth = 0;
        while (j < text.Length)
        {
            var ch = text[j];
            if (quote is not null)
            {
                if (ch == quote && text[j - 1] != '\\')
                    quote = null;
                j++;
                continue;
            }

            if (ch is '"' or '\'' or '`')
            {
                quote = ch;
                j++;
                continue;
            }

            if (ch == '(')
                depth++;
            else if (ch == ')' && depth > 0)
                depth--;
            else if (ch == '>' && depth == 0)
                break;
            j++;
        }

        if (j >= text.Length || text[j] != '>')
            return false;
        openEnd = j + 1;
        if (text[j - 1] == '/')
            return false;

        var nest = 1;
        var k = openEnd;
        while (k < text.Length && nest > 0)
        {
            var next = IndexOfTag(text, name, k);
            if (next < 0)
                return false;
            if (next + 1 < text.Length && text[next + 1] == '/')
            {
                nest--;
                if (nest == 0)
                {
                    closeStart = next;
                    var gt = text.IndexOf('>', next);
                    if (gt < 0)
                        return false;
                    closeEnd = gt + 1;
                    return true;
                }
            }
            else
            {
                nest++;
            }

            k = next + name.Length + 1;
        }

        return false;
    }

    private static int IndexOfTag(string text, string name, int from)
    {
        var open = "<" + name;
        var close = "</" + name;
        while (from < text.Length)
        {
            var openAt = text.IndexOf(open, from, StringComparison.OrdinalIgnoreCase);
            var closeAt = text.IndexOf(close, from, StringComparison.OrdinalIgnoreCase);
            if (openAt < 0 && closeAt < 0)
                return -1;
            var at = openAt < 0 ? closeAt : closeAt < 0 ? openAt : Math.Min(openAt, closeAt);
            var after = at + (at == closeAt ? close.Length : open.Length);
            if (after >= text.Length || !char.IsLetterOrDigit(text[after]))
                return at;
            from = at + 1;
        }

        return -1;
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
