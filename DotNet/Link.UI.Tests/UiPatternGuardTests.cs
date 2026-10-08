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
    private static readonly string[] ForbiddenHex =
    [
        "#0d6efd",
        "#0dcaf0",
        "#4da3ff",
        "#198754",
        "#084298",
        "#e8f3ff",
        "#2b86e6"
    ];

    private static readonly string[] ForbiddenMarkup =
    [
        "bg-primary",
        "text-primary",
        "btn-primary",
        "btn-outline-",
        "link-primary",
        "text-info",
        "bg-info",
        "nav-tabs"
    ];

    [Fact]
    public void Views_and_scripts_do_not_use_blue_or_the_old_green()
    {
        var hits = ProductFiles("*.cshtml", "Views")
            .Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js")))
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, index))
                .Where(row => ForbiddenHex.Any(token => row.line.Contains(token, StringComparison.OrdinalIgnoreCase))
                    || ForbiddenMarkup.Any(token => row.line.Contains(token, StringComparison.Ordinal))
                        && !row.line.Contains("patient-sort-button", StringComparison.Ordinal))
                .Select(row => Rel(row.file) + ":" + (row.index + 1)))
            .ToList();

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Shared_stylesheet_does_not_hard_code_blue_or_the_old_green()
    {
        var css = File.ReadAllText(Path.Combine(Root(), "wwwroot", "css", "site.css"));
        foreach (var token in ForbiddenHex)
            css.Contains(token, StringComparison.OrdinalIgnoreCase).Should().BeFalse(token);

        css.Should().Contain("--au-success:     #28a745;");
        css.Should().Contain("--au-accent:      #343a40;");
        css.Should().Contain(".lu-chart");
        css.Should().Contain(".lu-chart-donut");
        css.Should().Contain(".lu-logs-table");
        css.Should().Contain("min-width: 72rem");
        css.Should().Contain(".lu-reports-table");
        css.Should().Contain(".btn-check:checked + .btn-au-neutral");
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
        var run = File.ReadAllText(Path.Combine(Root(), "Views", "Automation", "_RunDetail.cshtml"));
        var manifest = File.ReadAllText(Path.Combine(Root(), "Views", "Automation", "Manifest.cshtml"));
        var normalizations = File.ReadAllText(Path.Combine(Root(), "Views", "Normalizations", "Index.cshtml"));
        var plans = File.ReadAllText(Path.Combine(Root(), "Views", "QueryPlans", "Index.cshtml"));
        foreach (var view in new[] { run, manifest, normalizations, plans })
        {
            view.Should().Contain("lu-section-nav");
            view.Should().NotContain("nav-tabs");
        }
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
        string[] required =
        [
            "--bs-primary:", "--bs-primary-rgb:", "--bs-link-color:", "--bs-link-color-rgb:",
            "--bs-link-hover-color:", "--bs-link-hover-color-rgb:", "--bs-code-color:", "--bs-focus-ring-color:",
            "--bs-info:", "--bs-info-rgb:",
            ".form-check-input:checked", ".form-check-input[type=checkbox]:indeterminate",
            ".form-switch .form-check-input:focus", "fill='%23111'",
            ".page-link", ".page-item.active .page-link", ".progress-bar",
            ".dropdown-item.active", ".list-group-item.active",
            ".alert-info", ".bg-success", ".bg-success-subtle", ".text-success-emphasis",
            "code, kbd, samp { color: #111; }"
        ];
        foreach (var token in required)
            css.Contains(token, StringComparison.Ordinal).Should().BeTrue(token);
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
            var folder = new DirectoryInfo(Path.GetDirectoryName(file)!).Name;
            var home = folder switch
            {
                "Logs" => "/Logs",
                "Configuration" => "/Configuration",
                "System" => "/System",
                _ => null
            };
            if (home is null)
                continue;

            var name = Path.GetFileNameWithoutExtension(file);
            foreach (var line in File.ReadAllLines(file))
            {
                if (!line.Contains("new BackLink", StringComparison.Ordinal) || !line.Contains("Url.Action(\"Index\")", StringComparison.Ordinal))
                    continue;
                var current = name.Equals("Index", StringComparison.OrdinalIgnoreCase) ? home : home + "/" + name;
                if (Link.UI.Services.BackLinkRules.Show(current, home, null))
                    hits.Add(Rel(file));
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
                .Where(row => row.line.Contains("toISOString", StringComparison.Ordinal)
                    && !row.line.Contains("sampledAt", StringComparison.Ordinal))
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
    }

    [Fact]
    public void Row_actions_are_icon_buttons()
    {
        string[] words = [">Delete</button>", ">Clone</button>", ">View</button>", ">Edit</button>", ">Remove</button>", ">&times;</button>"];
        var hits = ProductFiles("*.cshtml", "Views")
            .Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js")))
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, index))
                .Where(row => row.line.Contains("btn-sm", StringComparison.Ordinal)
                    && words.Any(word => row.line.Contains(word, StringComparison.Ordinal))
                    && !row.line.Contains("lu-icon-btn", StringComparison.Ordinal)
                    && !row.line.Contains("<i ", StringComparison.Ordinal))
                .Select(row => Rel(row.file) + ":" + (row.index + 1)))
            .ToList();

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

        var blue = h is >= 165 and <= 255 && s >= 0.28 && l is >= 0.08 and <= 0.92;
        var oldGreen = h is >= 145 and < 165 && s >= 0.40 && l is >= 0.22 and <= 0.60;
        return blue || oldGreen;
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
