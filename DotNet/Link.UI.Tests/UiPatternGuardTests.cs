using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

/// <summary>
/// Scans Link.UI views, scripts, and the shared stylesheet for patterns that
/// the shell is not allowed to grow back.
/// </summary>
public class UiPatternGuardTests
{
    [Fact]
    public void Shared_stylesheet_does_not_hard_code_blue_or_the_old_green()
    {
        var css = File.ReadAllText(Path.Combine(Root(), "wwwroot", "css", "site.css"));
        css.Should().Contain("--au-success:     #28a745;");
        css.Should().Contain("--au-accent:      #343a40;");
        css.Should().Contain(".lu-chart");
        css.Should().Contain(".lu-chart-donut");
        css.Should().Contain(".lu-logs-table");
        css.Should().Contain("min-width: 72rem");
        css.Should().Contain(".lu-reports-table");
        css.Should().Contain(".btn-check:checked + .btn-au-neutral");
        css.Should().Contain("--lu-chart-compact-height: 140px;");
        css.Should().Contain("--lu-chart-donut-size: 150px;");
        css.Should().Contain("--lu-chart-dashboard-height: 168px;");
        css.Should().Contain("--lu-chart-dashboard-donut: 168px;");
        css.Should().Contain("--lu-chart-dashboard-donut-box: 200px;");
        css.Should().Contain(".bg-light");
        css.Should().NotContain("max-height: 8.5rem");
        css.Should().NotContain("max-width: 120px");
        css.Should().Contain("#recentRunsCard .table-responsive");
        css.Should().Contain(".table { border-color: #e0e0e0; }");
    }

    [Fact]
    public void Recent_runs_refit_after_the_table_is_replaced()
    {
        var dash = File.ReadAllText(Path.Combine(Root(), "wwwroot", "js", "automation-dashboard.js"));
        var replaced = dash.IndexOf("host.innerHTML = html", StringComparison.Ordinal);
        replaced.Should().BeGreaterThan(0);
        var refit = dash.IndexOf("requestAnimationFrame(fitRecentRuns)", replaced, StringComparison.Ordinal);
        refit.Should().BeGreaterThan(replaced);
        dash.Should().Contain("quickLaunchCard");
        dash.Should().Contain(".lu-main");
    }

    [Fact]
    public void Back_controls_use_the_shared_partial()
    {
        var hits = ProductFiles("*.cshtml", "Views")
            .Where(file => !file.EndsWith("_BackButton.cshtml", StringComparison.OrdinalIgnoreCase))
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, index))
                .Where(row => row.line.Contains("bi-arrow-left", StringComparison.Ordinal)
                    && !row.line.Contains("bi-arrow-left-right", StringComparison.Ordinal))
                .Select(row => Rel(row.file) + ":" + (row.index + 1)))
            .ToList();

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Tab_strips_use_the_shared_bar()
    {
        var hits = new List<string>();
        foreach (var file in ProductFiles("*.cshtml", "Views"))
        {
            var text = File.ReadAllText(file);
            if (text.Contains("nav-tabs", StringComparison.Ordinal) || text.Contains("nav-pills", StringComparison.Ordinal))
                hits.Add(Rel(file) + " uses a vendor tab strip");
            if (text.Contains("data-bs-toggle=\"tab\"", StringComparison.Ordinal)
                && !text.Contains("lu-section-nav", StringComparison.Ordinal))
                hits.Add(Rel(file) + " has a tab strip outside the shared bar");
        }

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Acquisition_logs_keep_the_shared_actions()
    {
        var list = File.ReadAllText(Path.Combine(Root(), "Views", "Logs", "_AcquisitionLogList.cshtml"));
        list.Should().Contain("StatusPills.ForLog");
        list.Should().Contain("ProcessOne");
        list.Should().Contain("ReturnUrlRules.Sanitize");
        list.Should().Contain("lu-logs-table");
        list.Should().NotContain("<ul");
    }

    [Fact]
    public void Log_and_report_tables_truncate()
    {
        File.ReadAllText(Path.Combine(Root(), "Views", "Logs", "_AcquisitionLogList.cshtml"))
            .Should().Contain("lu-col-created");
        File.ReadAllText(Path.Combine(Root(), "Views", "Reports", "Index.cshtml"))
            .Should().Contain("lu-reports-table");
        File.ReadAllText(Path.Combine(Root(), "Views", "Reports", "Index.cshtml"))
            .Should().Contain("lu-measures");
        File.ReadAllText(Path.Combine(Root(), "Views", "Reports", "Validation.cshtml"))
            .Should().Contain("lu-issue-filters");
        File.ReadAllText(Path.Combine(Root(), "Views", "Reports", "Validation.cshtml"))
            .Should().Contain("col-category");
    }

    [Fact]
    public void Colors_are_not_blue_cyan_or_the_old_green()
    {
        var hits = SourceLines()
            .Where(row => !IsSharedChartPalette(row.Where))
            .SelectMany(row => ColorsIn(row.Line).Where(IsForbiddenHue).Select(color => row.Where + " " + color))
            .ToList();

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Views_and_scripts_do_not_use_forbidden_color_classes()
    {
        string[] forbidden =
        [
            "alert-info", "text-bg-info", "btn-info", "link-info", "btn-link",
            "bg-primary", "text-primary", "btn-primary", "link-primary", "border-primary",
            "alert-primary", "text-bg-primary", "table-primary", "list-group-item-primary",
            "bg-info", "text-info", "border-info", "table-info", "list-group-item-info",
            "btn-outline-"
        ];
        var hits = ProductFiles("*.cshtml", "Views")
            .Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js")))
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, index))
                .Where(row => forbidden.Any(token => row.line.Contains(token, StringComparison.Ordinal)))
                .Select(row => Rel(row.file) + ":" + (row.index + 1)))
            .ToList();

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Stylesheet_overrides_every_vendor_blue_state()
    {
        var css = File.ReadAllText(Path.Combine(Root(), "wwwroot", "css", "site.css"));
        string[] selectors =
        [
            ".form-check-input:checked",
            ".form-check-input[type=checkbox]:indeterminate",
            ".form-switch .form-check-input:focus",
            ".page-link",
            ".page-item.active .page-link",
            ".progress-bar",
            ".dropdown-item.active",
            ".list-group-item.active",
            ".alert-info",
            ".bg-success",
            ".bg-success-subtle",
            ".text-success-emphasis"
        ];
        var rules = CssRules(css).ToList();
        foreach (var selector in selectors)
        {
            var bodies = rules.Where(rule => SelectorHas(rule.Selector, selector)).Select(rule => rule.Body).ToList();
            bodies.Should().NotBeEmpty(selector);
            var body = string.Join("\n", bodies);
            body.Contains("#0d6efd", StringComparison.OrdinalIgnoreCase).Should().BeFalse(selector);
            body.Contains("#0dcaf0", StringComparison.OrdinalIgnoreCase).Should().BeFalse(selector);
            body.Contains("#198754", StringComparison.OrdinalIgnoreCase).Should().BeFalse(selector);
            body.Contains("#86b7fe", StringComparison.OrdinalIgnoreCase).Should().BeFalse(selector);
            Regex.IsMatch(body, @"#111\b|#fff\b|var\(--au-|var\(--bs-", RegexOptions.IgnoreCase)
                .Should().BeTrue(selector + " must set a token color");
        }

        var root = rules.First(rule => SelectorHas(rule.Selector, ":root")).Body;
        root.Should().Contain("--bs-primary:");
        root.Should().Contain("#111");
        root.Should().Contain("--bs-link-color:");
        root.Should().Contain("--bs-info:");
        css.Should().Contain("fill='%23111'");
    }

    [Fact]
    public void Status_badges_use_the_shared_map()
    {
        string[] bootstrap = ["bg-success", "bg-danger", "bg-warning", "bg-info", "bg-primary", "bg-secondary"];
        var hits = ProductFiles("*.cshtml", "Views")
            .Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js")))
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, index))
                .Where(row => row.line.Contains("badge", StringComparison.Ordinal)
                    && bootstrap.Any(token => row.line.Contains(token, StringComparison.Ordinal)))
                .Select(row => Rel(row.file) + ":" + (row.index + 1) + " " + row.line.Trim()))
            .ToList();

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Displayed_ids_use_the_labeled_copy_control()
    {
        var hits = ProductFiles("*.cshtml", "Views")
            .Where(file => !file.EndsWith("_LabeledId.cshtml", StringComparison.OrdinalIgnoreCase))
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, index))
                .Where(row => row.line.Contains("<code", StringComparison.Ordinal)
                    && !row.line.Contains("lu-facility-id", StringComparison.Ordinal)
                    && Regex.IsMatch(row.line, @"\b(Id|pid|patientId|FacilityId|ResourceId|CorrelationId)\b"))
                .Select(row => Rel(row.file) + ":" + (row.index + 1)))
            .ToList();

        hits.Should().BeEmpty();
        File.ReadAllText(Path.Combine(Root(), "Views", "Shared", "_Layout.cshtml"))
            .Should().Contain("window.luLabeledId");
    }

    [Fact]
    public void Back_controls_hide_a_self_or_same_section_target()
    {
        File.ReadAllText(Path.Combine(Root(), "Views", "Shared", "_BackButton.cshtml"))
            .Should().Contain("BackLinkRules.Show")
            .And.Contain("BackLinkRules.LabelFor");

        var hits = new List<string>();
        foreach (var file in ProductFiles("*.cshtml", "Views"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.StartsWith("_", StringComparison.Ordinal))
                continue;

            var folder = new DirectoryInfo(Path.GetDirectoryName(file)!).Name;
            var current = name.Equals("Index", StringComparison.OrdinalIgnoreCase) ? "/" + folder : "/" + folder + "/" + name;
            foreach (var line in File.ReadAllLines(file))
            {
                if (!line.Contains("new BackLink", StringComparison.Ordinal))
                    continue;

                var label = Regex.Match(line, "\"([^\"]*)\"\\s*\\)");
                if (label.Success)
                {
                    var rendered = Link.UI.Services.BackLinkRules.LabelFor(null, null, label.Groups[1].Value);
                    if (!rendered.StartsWith("Back to ", StringComparison.Ordinal))
                        hits.Add(Rel(file) + " label " + rendered);
                }

                var action = Regex.Match(line, "Url\\.Action\\(\\s*\"(\\w+)\"(?:\\s*,\\s*\"(\\w+)\")?");
                if (!action.Success)
                    continue;
                var controller = action.Groups[2].Success ? action.Groups[2].Value : folder;
                var targetAction = action.Groups[1].Value;
                var target = targetAction.Equals("Index", StringComparison.OrdinalIgnoreCase)
                    ? "/" + controller
                    : "/" + controller + "/" + targetAction;
                var samePage = string.Equals(current, target, StringComparison.OrdinalIgnoreCase);
                var sameSection = Link.UI.Services.BackLinkRules.SectionHome(current) is string home
                    && string.Equals(home, target, StringComparison.OrdinalIgnoreCase);
                if ((samePage || sameSection) && Link.UI.Services.BackLinkRules.Show(current, target, null))
                    hits.Add(Rel(file) + " back target " + target);
            }
        }

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Section_tabs_use_the_shared_bar()
    {
        var hits = ProductFiles("*.cshtml", "Views")
            .Where(file => File.ReadAllText(file).Contains("data-bs-toggle=\"tab\"", StringComparison.Ordinal)
                && !File.ReadAllText(file).Contains("lu-section-nav", StringComparison.Ordinal))
            .Select(Rel)
            .ToList();

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Display_times_are_not_raw_utc_strings()
    {
        var hits = ProductFiles("*.cshtml", "Views")
            .Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js")))
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, index))
                .Where(row => IsRawUtc(row.line))
                .Select(row => Rel(row.file) + ":" + (row.index + 1)))
            .ToList();

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Table_filters_stay_in_the_query_string()
    {
        var js = File.ReadAllText(Path.Combine(Root(), "wwwroot", "js", "au-data-table.js"));
        js.Should().Contain("URLSearchParams");
        js.Should().Contain("history.replaceState");
        js.Should().Contain("popstate");
        js.Should().Contain("params.get('q')");
        js.Should().Contain("params.get('sort')");
        js.Should().NotContain("sortBy");
        js.Should().NotContain("pageNumber");

        var query = File.ReadAllText(Path.Combine(Root(), "wwwroot", "js", "lu-query.js"));
        query.Should().Contain("history.replaceState");
        query.Should().Contain("popstate");
        query.Should().Contain("[data-lu-query]");

        var hits = new List<string>();
        foreach (var file in ProductFiles("*.cshtml", "Views"))
        {
            var text = File.ReadAllText(file);
            if (text.Contains("data-au-table", StringComparison.Ordinal) && !text.Contains("au-data-table.js", StringComparison.Ordinal))
            {
                var name = Path.GetFileName(file);
                var folder = Path.GetDirectoryName(file)!;
                var covered = name.StartsWith("_", StringComparison.Ordinal)
                    && Directory.EnumerateFiles(folder, "*.cshtml").Any(other => File.ReadAllText(other).Contains("au-data-table.js", StringComparison.Ordinal));
                if (!covered)
                    hits.Add(Rel(file) + " has a table filter without the shared query script");
            }

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("<input", StringComparison.OrdinalIgnoreCase))
                    continue;
                var tag = lines[i];
                var end = i;
                while (!tag.Contains('>', StringComparison.Ordinal) && end + 1 < lines.Length)
                    tag += " " + lines[++end];
                var search = tag.Contains("type=\"search\"", StringComparison.Ordinal)
                    || Regex.IsMatch(tag, "id=\"[^\"]*Search\"");
                if (!search)
                    continue;
                if (tag.Contains("name=", StringComparison.Ordinal) || tag.Contains("data-lu-query", StringComparison.Ordinal))
                    continue;
                if (tag.Contains("quickLaunchSearch", StringComparison.Ordinal))
                    continue;
                hits.Add(Rel(file) + " " + lines[i].Trim());
            }
        }

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Row_actions_are_icon_buttons()
    {
        string[] words = [">Delete</button>", ">Clone</button>", ">View</button>", ">Edit</button>", ">Remove</button>", ">&times;</button>", ">Save</button>", ">Restore</button>"];
        var hits = new List<string>();
        foreach (var file in ProductFiles("*.cshtml", "Views").Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js"))))
        {
            var inCell = false;
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Contains("<td", StringComparison.Ordinal) || line.Contains("lu-row-actions", StringComparison.Ordinal))
                    inCell = true;
                if (inCell
                    && line.Contains("btn-sm", StringComparison.Ordinal)
                    && words.Any(word => line.Contains(word, StringComparison.Ordinal))
                    && !line.Contains("lu-icon-btn", StringComparison.Ordinal)
                    && !line.Contains("<i ", StringComparison.Ordinal))
                    hits.Add(Rel(file) + ":" + (i + 1));
                if (line.Contains("</td", StringComparison.Ordinal) || line.Contains("</tr", StringComparison.Ordinal))
                    inCell = false;
            }
        }

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Identifiers_truncate_instead_of_wrapping_per_character()
    {
        var css = File.ReadAllText(Path.Combine(Root(), "wwwroot", "css", "site.css"));
        var facility = css.Split(".lu-facility-id,", 2)[1].Split('}', 2)[0];
        facility.Should().Contain("text-overflow: ellipsis");
        facility.Should().Contain("white-space: nowrap");
        facility.Should().NotContain("break-all");

        var hits = new List<string>();
        foreach (var file in ProductFiles("*.cshtml", "Views"))
        {
            var text = File.ReadAllText(file);
            foreach (Match table in Regex.Matches(text, @"<table\b[\s\S]*?</table>", RegexOptions.IgnoreCase))
            {
                var block = table.Value;
                var visible = Regex.Replace(block, "=\"[^\"]*\"", "=\"\"");
                visible = Regex.Replace(visible, "='[^']*'", "=''");
                var rendersId = visible.Contains("_LabeledId", StringComparison.Ordinal)
                    || visible.Contains("luLabeledId", StringComparison.Ordinal)
                    || Regex.IsMatch(visible, @"\b(?:FacilityId|ReportId|CorrelationId|PatientId)\b");
                if (!rendersId)
                    continue;
                var truncates = block.Contains("_LabeledId", StringComparison.Ordinal)
                    || block.Contains("luLabeledId", StringComparison.Ordinal)
                    || block.Contains("lu-facility-id", StringComparison.Ordinal)
                    || block.Contains("lu-clip", StringComparison.Ordinal)
                    || block.Contains("lu-logs-table", StringComparison.Ordinal)
                    || block.Contains("lu-reports-table", StringComparison.Ordinal);
                if (!truncates)
                    hits.Add(Rel(file));
            }
        }

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Status_text_uses_the_shared_helper()
    {
        var localHelper = new Regex(@"function\s+\w*(?:Badge|badgeClass|statusPill|StatusClass)\w*\s*\(", RegexOptions.IgnoreCase);
        var toneClass = new Regex(@"au-badge-(?:success|warning|danger|muted|active|skip)|bg-danger|bg-warning|bg-secondary", RegexOptions.IgnoreCase);
        var mappedTone = new Regex(@"\?\s*['""](?:au-badge-|bg-danger|bg-warning|bg-secondary)");
        var hits = new List<string>();
        foreach (var file in ProductFiles("*.cshtml", "Views").Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js"))))
        {
            if (file.EndsWith("status-pills.js", StringComparison.OrdinalIgnoreCase))
                continue;
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (localHelper.IsMatch(line))
                {
                    var body = FunctionBody(lines, i);
                    var usesShared = body.Contains("StatusPills", StringComparison.Ordinal) || body.Contains("luStatusPills", StringComparison.Ordinal);
                    var distinct = toneClass.Matches(body).Select(match => match.Value.ToLowerInvariant()).Distinct().Count();
                    if (!usesShared && distinct >= 2)
                        hits.Add(Rel(file) + ":" + (i + 1) + " " + line.Trim());
                }
                if (!mappedTone.IsMatch(line))
                    continue;
                if (line.Contains("StatusPills", StringComparison.Ordinal) || line.Contains("luStatusPills", StringComparison.Ordinal))
                    continue;
                if (Regex.IsMatch(line, @"status|outcome|level|passed|failed|succeeded|running", RegexOptions.IgnoreCase))
                    hits.Add(Rel(file) + ":" + (i + 1) + " " + line.Trim());
            }
        }

        hits.Should().BeEmpty();
        File.ReadAllText(Path.Combine(Root(), "wwwroot", "js", "status-pills.js")).Should().Contain("window.luStatusPills");
        File.ReadAllText(Path.Combine(Root(), "Services", "StatusPills.cs")).Should().Contain("ForRun");
    }

    [Fact]
    public void Chart_colors_come_from_the_shared_palette()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var color in ColorsIn(File.ReadAllText(Path.Combine(Root(), "wwwroot", "js", "chart-palette.js"))))
            allowed.Add(color);
        var rootRule = CssRules(File.ReadAllText(Path.Combine(Root(), "wwwroot", "css", "site.css")))
            .First(rule => SelectorHas(rule.Selector, ":root"));
        foreach (var color in ColorsIn(rootRule.Body))
            allowed.Add(color);

        var chartLine = new Regex(@"backgroundColor|borderColor|chartColors|fallbackPalette");
        var reserved = new[] { "#6f42c1", "#fd7e14", "#e83e8c" };
        var hits = new List<string>();
        foreach (var row in SourceLines())
        {
            if (IsSharedChartPalette(row.Where))
                continue;
            if (reserved.Any(color => row.Line.Contains(color, StringComparison.OrdinalIgnoreCase)))
                hits.Add(row.Where + " " + row.Line.Trim());
            if (!chartLine.IsMatch(row.Line))
                continue;
            foreach (var color in ColorsIn(row.Line))
            {
                if (!allowed.Contains(color))
                    hits.Add(row.Where + " " + color);
            }
        }

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Chart_canvases_use_the_shared_frame()
    {
        var hits = new List<string>();
        foreach (var file in ProductFiles("*.cshtml", "Views"))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("<canvas", StringComparison.Ordinal))
                    continue;
                var start = Math.Max(0, i - 3);
                var window = string.Join("\n", lines.Skip(start).Take(i - start + 1));
                if (!window.Contains("lu-chart", StringComparison.Ordinal) && !window.Contains("au-trend", StringComparison.Ordinal))
                    hits.Add(Rel(file) + ":" + (i + 1));
            }
        }

        foreach (var file in ProductFiles("*.cshtml", "Views").Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js"))))
        {
            var text = File.ReadAllText(file);
            if (text.Contains("new Chart", StringComparison.Ordinal) && !text.Contains("luChartFrame", StringComparison.Ordinal))
                hits.Add(Rel(file) + " draws a chart without the shared frame");
        }

        hits.Should().BeEmpty();
        File.ReadAllText(Path.Combine(Root(), "wwwroot", "js", "chart-frame.js")).Should().Contain("No data yet.");
    }

    [Fact]
    public void Detail_headers_do_not_repeat_an_identifier()
    {
        File.ReadAllText(Path.Combine(Root(), "Views", "Shared", "_LabeledId.cshtml"))
            .Should().Contain("LabeledIdRules.DisplayName");
        var run = File.ReadAllText(Path.Combine(Root(), "Views", "Automation", "_RunDetail.cshtml"));
        Regex.Matches(run, "Label = \"Report ID\"").Count.Should().Be(1);
        run.Should().NotContain("scheduleFacilityId");
        run.Should().NotContain("scheduleReportName");
        var validation = File.ReadAllText(Path.Combine(Root(), "Views", "Reports", "Validation.cshtml"));
        Regex.Matches(validation, "_ReportIdentity").Count.Should().Be(1);
        validation.Should().NotContain("Label = \"Facility\"");
    }

    private static bool IsSharedChartPalette(string where)
    {
        var path = where.Replace('\\', '/');
        var slash = path.LastIndexOf('/');
        var colon = path.LastIndexOf(':');
        if (colon > slash)
            path = path[..colon];
        return path.EndsWith("wwwroot/js/chart-palette.js", StringComparison.OrdinalIgnoreCase);
    }

    private static string FunctionBody(string[] lines, int start)
    {
        var body = "";
        var depth = 0;
        var opened = false;
        var limit = Math.Min(lines.Length, start + 80);
        for (var i = start; i < limit; i++)
        {
            body += lines[i] + "\n";
            foreach (var ch in lines[i])
            {
                if (ch == '{')
                {
                    depth++;
                    opened = true;
                }
                else if (ch == '}')
                {
                    depth--;
                }
            }

            if (opened && depth <= 0)
                break;
        }

        return body;
    }

    private static bool IsRawUtc(string line)
    {
        if (line.Contains("toUTCString", StringComparison.Ordinal))
            return true;
        if (line.Contains("ToString(\"u\")", StringComparison.Ordinal))
            return true;
        if (line.Contains("ToUniversalTime", StringComparison.Ordinal))
            return true;
        if (line.Contains("yyyy-MM-dd HH:mm:ssZ", StringComparison.Ordinal))
            return true;
        return line.Contains("toISOString", StringComparison.Ordinal)
            && !line.Contains("sampledAt", StringComparison.Ordinal);
    }

    private static IEnumerable<(string Selector, string Body)> CssRules(string css)
    {
        var stripped = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
        foreach (Match match in Regex.Matches(stripped, @"([^{}]+)\{([^{}]*)\}"))
            yield return (match.Groups[1].Value, match.Groups[2].Value);
    }

    private static bool SelectorHas(string selectorList, string selector)
    {
        foreach (var part in selectorList.Split(','))
        {
            if (part.Contains(selector, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static IEnumerable<(string Where, string Line)> SourceLines()
    {
        foreach (var file in ProductFiles("*.cshtml", "Views").Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js"))))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
                yield return (Rel(file) + ":" + (i + 1), lines[i]);
        }

        var css = File.ReadAllLines(Path.Combine(Root(), "wwwroot", "css", "site.css"));
        for (var i = 0; i < css.Length; i++)
            yield return ("wwwroot/css/site.css:" + (i + 1), css[i]);
    }

    private static IEnumerable<string> ColorsIn(string line)
    {
        foreach (Match match in Regex.Matches(line, @"#([0-9a-fA-F]{3,8})\b|%23([0-9a-fA-F]{6})|rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})|hsla?\(\s*([0-9.]+)\s*,\s*([0-9.]+)%\s*,\s*([0-9.]+)%"))
        {
            if (match.Groups[1].Success)
                yield return "#" + match.Groups[1].Value;
            else if (match.Groups[2].Success)
                yield return "#" + match.Groups[2].Value;
            else if (match.Groups[3].Success)
                yield return "rgb(" + match.Groups[3].Value + "," + match.Groups[4].Value + "," + match.Groups[5].Value + ")";
            else
                yield return "hsl(" + match.Groups[6].Value + "," + match.Groups[7].Value + "%," + match.Groups[8].Value + "%)";
        }
    }

    private static bool IsForbiddenHue(string color)
    {
        double h, s, l;
        if (color.StartsWith('#'))
        {
            var hex = color[1..];
            if (hex.Length == 8)
                hex = hex[..6];
            else if (hex.Length == 4)
                hex = hex[..3];
            if (hex.Length == 3)
                hex = string.Concat(hex.Select(ch => new string(ch, 2)));
            if (hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var packed))
                return false;
            (h, s, l) = RgbToHsl((packed >> 16) & 255, (packed >> 8) & 255, packed & 255);
        }
        else if (color.StartsWith("rgb", StringComparison.Ordinal))
        {
            var parts = color[4..^1].Split(',');
            (h, s, l) = RgbToHsl(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
        }
        else
        {
            var parts = color[4..^1].Split(',');
            h = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
            s = double.Parse(parts[1].TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture) / 100d;
            l = double.Parse(parts[2].TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture) / 100d;
        }

        var cool = h is >= 190 and <= 340 && s >= 0.12;
        var oldGreen = h is >= 145 and < 165 && s >= 0.40;
        return cool || oldGreen;
    }

    private static (double H, double S, double L) RgbToHsl(int r, int g, int b)
    {
        var red = r / 255d;
        var green = g / 255d;
        var blue = b / 255d;
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var l = (max + min) / 2d;
        var delta = max - min;
        if (delta < 0.00001)
            return (0, 0, l);

        var s = delta / (1d - Math.Abs((2d * l) - 1d));
        double h;
        if (Math.Abs(max - red) < 0.00001)
            h = 60d * (((green - blue) / delta) % 6d);
        else if (Math.Abs(max - green) < 0.00001)
            h = 60d * (((blue - red) / delta) + 2d);
        else
            h = 60d * (((red - green) / delta) + 4d);
        if (h < 0)
            h += 360d;
        return (h, s, l);
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
