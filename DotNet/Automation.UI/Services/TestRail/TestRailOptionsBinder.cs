using System.Globalization;
using Automation.UI.Models;

namespace Automation.UI.Services.TestRail;

/// <summary>
/// Reads <see cref="TestRailOptions"/> from configuration without throwing. The default binder
/// throws when a number setting holds text, and the options are read at startup, so one bad
/// value would stop the service. Here every value is read as text and checked:
/// <list type="bullet">
/// <item>Surrounding whitespace and one pair of surrounding double quotes are removed, so
/// <c>""</c> is blank and <c>"12"</c> is 12.</item>
/// <item>ProjectId accepts 12, P12, or a project name. Suite ids accept 34, S34, or a suite name.
/// Names are looked up in TestRail when results are published.</item>
/// <item>BaseUrl is the site root. A trailing <c>/index.php?/api/v2</c> is removed.</item>
/// </list>
/// Anything that cannot be read is left unset and described in
/// <see cref="TestRailOptions.ConfigurationProblems"/>. The API key value is never described.
/// </summary>
public static class TestRailOptionsBinder
{
    private static readonly string[] ApiSuffixes = ["/index.php?/api/v2", "/index.php?", "/index.php"];

    public static TestRailOptions Bind(IConfiguration section)
    {
        var options = new TestRailOptions();
        Bind(section, options);
        return options;
    }

    public static void Bind(IConfiguration section, TestRailOptions options)
    {
        var problems = options.ConfigurationProblems;
        problems.Clear();

        options.Enabled = ReadBool(section, nameof(TestRailOptions.Enabled), false, problems);
        options.BaseUrl = ReadBaseUrl(section, problems);
        options.Username = Clean(section[nameof(TestRailOptions.Username)]);
        options.ApiKey = Clean(section[nameof(TestRailOptions.ApiKey)]);

        (options.ProjectId, options.ProjectName) =
            ReadIdOrName(section, nameof(TestRailOptions.ProjectId), 'P', problems);
        (options.ScenarioSuiteId, options.ScenarioSuiteName) =
            ReadIdOrName(section, nameof(TestRailOptions.ScenarioSuiteId), 'S', problems);
        (options.ApiHealthSuiteId, options.ApiHealthSuiteName) =
            ReadIdOrName(section, nameof(TestRailOptions.ApiHealthSuiteId), 'S', problems);

        var timeout = ReadInt(section, nameof(TestRailOptions.RequestTimeoutSeconds), problems);
        options.RequestTimeoutSeconds = timeout is > 0 ? timeout.Value : TestRailOptions.DefaultRequestTimeoutSeconds;

        options.UseSharedRun = ReadBool(section, nameof(TestRailOptions.UseSharedRun), false, problems);
        options.SharedScenarioRunId = ReadRunId(section, nameof(TestRailOptions.SharedScenarioRunId), problems);
        options.SharedApiHealthRunId = ReadRunId(section, nameof(TestRailOptions.SharedApiHealthRunId), problems);
        options.SkipStatusId = ReadInt(section, nameof(TestRailOptions.SkipStatusId), problems) ?? 0;

        options.ScenarioCaseIds = ReadCaseMap(section, nameof(TestRailOptions.ScenarioCaseIds), problems);
        options.ApiHealthCaseIds = ReadCaseMap(section, nameof(TestRailOptions.ApiHealthCaseIds), problems);
    }

    /// <summary>Trims whitespace and one pair of surrounding double quotes.</summary>
    internal static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = value.Trim();
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
            text = text[1..^1].Trim();
        return text;
    }

    private static bool ReadBool(IConfiguration section, string key, bool fallback, List<string> problems)
    {
        var text = Clean(section[key]);
        if (text.Length == 0)
            return fallback;
        if (bool.TryParse(text, out var value))
            return value;

        problems.Add($"{Path(key)} must be true or false. It is treated as {fallback.ToString().ToLowerInvariant()}.");
        return fallback;
    }

    private static int? ReadInt(IConfiguration section, string key, List<string> problems)
    {
        var text = Clean(section[key]);
        if (text.Length == 0)
            return null;
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return value;

        problems.Add($"{Path(key)} must be a whole number. It is ignored.");
        return null;
    }

    private static int? ReadRunId(IConfiguration section, string key, List<string> problems)
    {
        var text = Clean(section[key]);
        if (text.Length == 0)
            return null;
        if (TryParsePrefixedId(text, 'R', out var id))
            return id;

        problems.Add($"{Path(key)} must be a TestRail run id such as 123 or R123. It is ignored.");
        return null;
    }

    private static (int Id, string Name) ReadIdOrName(
        IConfiguration section,
        string key,
        char prefix,
        List<string> problems)
    {
        var text = Clean(section[key]);
        if (text.Length == 0)
            return (0, string.Empty);
        if (TryParsePrefixedId(text, prefix, out var id))
            return (id, string.Empty);
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number <= 0)
            return (0, string.Empty);

        return (0, text);
    }

    private static string ReadBaseUrl(IConfiguration section, List<string> problems)
    {
        var text = Clean(section[nameof(TestRailOptions.BaseUrl)]).TrimEnd('/');
        if (text.Length == 0)
            return string.Empty;

        foreach (var suffix in ApiSuffixes)
        {
            if (text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                text = text[..^suffix.Length].TrimEnd('/');
                break;
            }
        }

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            && string.IsNullOrEmpty(uri.Query))
        {
            return text;
        }

        problems.Add($"{Path(nameof(TestRailOptions.BaseUrl))} must be the TestRail site address, for example https://example.testrail.io. Publishing stays off.");
        return string.Empty;
    }

    private static Dictionary<string, int> ReadCaseMap(IConfiguration section, string key, List<string> problems)
    {
        // API Health endpoint keys contain "::", which configuration splits into sections, so the
        // map key is the full path below the map rather than the first child name.
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (entryKey, rawValue) in section.GetSection(key).AsEnumerable(makePathsRelative: true))
        {
            if (rawValue is null || string.IsNullOrEmpty(entryKey))
                continue;

            var text = Clean(rawValue);
            if (TestRailCaseIdParser.TryParse(text, out var caseId) && caseId is int value)
            {
                map[entryKey] = value;
                continue;
            }

            if (text.Length > 0)
                problems.Add($"{Path(key)}:{entryKey} must be a TestRail case id such as 123 or C123. It is ignored.");
        }

        return map;
    }

    private static bool TryParsePrefixedId(string text, char prefix, out int id)
    {
        id = 0;
        var span = text.AsSpan();
        if (span.Length > 1 && char.ToUpperInvariant(span[0]) == prefix)
            span = span[1..];

        return int.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;
    }

    private static string Path(string key) => $"{TestRailOptions.SectionName}:{key}";
}
