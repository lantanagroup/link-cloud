using System.Globalization;
using System.Text;
using LantanaGroup.Link.Automation.Link.Models;

namespace Automation.UI.Services.TestRail;

/// <summary>
/// Builds TestRail run and test URLs from the configured site root and records
/// the runs a publish actually posted to.
/// </summary>
public static class TestRailRunLinks
{
    public const string NotPublished = "Not published";

    private static readonly string[] BaseSuffixes = ["/index.php?/api/v2", "/index.php?", "/index.php"];

    public static TestRailRunReference? Capture(string? baseUrl, int runId, IReadOnlyList<TestRailResultDto>? posted)
    {
        if (posted == null || posted.Count == 0)
            return null;

        var url = RunUrl(baseUrl, runId);
        if (url == null)
            return null;

        int? testId = null;
        string? testUrl = null;
        var testIds = (posted ?? [])
            .Select(result => result.TestId)
            .Where(id => id > 0)
            .Distinct()
            .Take(2)
            .ToList();
        if (testIds.Count == 1)
        {
            testId = testIds[0];
            testUrl = TestUrl(baseUrl, testId.Value);
        }

        return new TestRailRunReference
        {
            RunId = runId,
            Url = url,
            TestId = testId,
            TestUrl = testUrl
        };
    }

    public static string? RunUrl(string? baseUrl, int runId) =>
        runId > 0 ? PageUrl(baseUrl, "runs/view/" + runId.ToString(CultureInfo.InvariantCulture)) : null;

    public static string? TestUrl(string? baseUrl, int testId) =>
        testId > 0 ? PageUrl(baseUrl, "tests/view/" + testId.ToString(CultureInfo.InvariantCulture)) : null;

    public static bool IsDisplayable(TestRailRunReference? run) =>
        run is { RunId: > 0 } && IsHttpUrl(run.Url);

    public static bool IsHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
            return false;
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    /// <summary>
    /// Adds runs that are not already stored. The same run id is kept once.
    /// A later post can fill in the test link when the stored row does not have one.
    /// </summary>
    public static bool AddDistinct(List<TestRailRunReference> target, IEnumerable<TestRailRunReference>? added)
    {
        ArgumentNullException.ThrowIfNull(target);
        var changed = false;
        foreach (var run in added ?? [])
        {
            if (!IsDisplayable(run))
                continue;

            var index = target.FindIndex(existing => existing.RunId == run.RunId);
            if (index < 0)
            {
                target.Add(Clone(run));
                changed = true;
                continue;
            }

            if (target[index].TestId is not > 0 && run.TestId is > 0 && IsHttpUrl(run.TestUrl))
            {
                target[index] = Clone(run);
                changed = true;
            }
        }

        return changed;
    }

    public static List<TestRailRunReference> Copy(IReadOnlyList<TestRailRunReference>? runs)
    {
        if (runs == null || runs.Count == 0)
            return [];

        var copy = new List<TestRailRunReference>(runs.Count);
        foreach (var run in runs)
        {
            if (IsDisplayable(run))
                copy.Add(Clone(run));
        }

        return copy;
    }

    public static void AppendExport(StringBuilder sb, IReadOnlyList<TestRailRunReference>? runs)
    {
        var distinct = Copy(runs);
        if (distinct.Count == 0)
        {
            AppendKvp(sb, "TestRail", NotPublished);
            return;
        }

        foreach (var run in distinct)
        {
            AppendKvp(sb, "TestRail Run", run.RunId.ToString(CultureInfo.InvariantCulture));
            AppendKvp(sb, "TestRail URL", run.Url);
            if (run.TestId is > 0 && IsHttpUrl(run.TestUrl))
                AppendKvp(sb, "TestRail Test", run.TestUrl);
        }
    }

    private static void AppendKvp(StringBuilder sb, string key, string? value) =>
        sb.AppendLine($"{key,-26}: {value ?? "-"}");

    private static TestRailRunReference Clone(TestRailRunReference run) => new()
    {
        RunId = run.RunId,
        Url = run.Url.Trim(),
        TestId = run.TestId is > 0 ? run.TestId : null,
        TestUrl = run.TestId is > 0 && IsHttpUrl(run.TestUrl) ? run.TestUrl!.Trim() : null
    };

    private static string? PageUrl(string? baseUrl, string page)
    {
        var root = SiteRoot(baseUrl);
        if (root == null)
            return null;
        return root + "/index.php?/" + page;
    }

    internal static string? SiteRoot(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return null;

        var text = baseUrl.Trim().TrimEnd('/');
        var stripped = true;
        while (stripped && text.Length > 0)
        {
            stripped = false;
            foreach (var suffix in BaseSuffixes)
            {
                if (!text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    continue;
                text = text[..^suffix.Length].TrimEnd('/');
                stripped = true;
                break;
            }
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;
        if (!string.IsNullOrEmpty(uri.UserInfo))
            return null;
        return text;
    }
}
