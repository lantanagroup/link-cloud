namespace Automation.UI.Services.TestRail;

/// <summary>
/// Startup log for TestRail publishing: one summary line, then one warning per setting that
/// could not be read or is missing. The user name and API key are reported only as set or not set.
/// </summary>
public static class TestRailStartupReport
{
    public static void Log(ILogger logger, TestRailOptions options)
    {
        var missing = options.MissingRequiredSettings();
        var state = !options.Enabled
            ? "disabled"
            : missing.Count == 0 ? "enabled" : "enabled but incomplete, so off";

        logger.LogInformation(
            "TestRail publishing is {TestRailPublishingState}. BaseUrl {BaseUrl}, Username {UsernameState}, ApiKey {ApiKeyState}, Project {Project}, ScenarioSuite {ScenarioSuite}, ApiHealthSuite {ApiHealthSuite}.",
            state,
            string.IsNullOrWhiteSpace(options.BaseUrl) ? "not set" : options.BaseUrl,
            string.IsNullOrWhiteSpace(options.Username) ? "not set" : "set",
            string.IsNullOrWhiteSpace(options.ApiKey) ? "not set" : "set",
            Describe(options.ProjectId, options.ProjectName),
            Describe(options.ScenarioSuiteId, options.ScenarioSuiteName),
            Describe(options.ApiHealthSuiteId, options.ApiHealthSuiteName));

        foreach (var problem in options.ConfigurationProblems)
            logger.LogWarning("TestRail setting problem: {Problem}", problem);

        if (!options.Enabled)
            return;

        if (missing.Count > 0)
        {
            logger.LogWarning(
                "TestRail publishing is enabled but these settings are missing: {MissingSettings}. Nothing is published.",
                string.Join(", ", missing.Select(m => $"{TestRailOptions.SectionName}:{m}")));
            return;
        }

        if (!options.HasScenarioSuite)
        {
            logger.LogWarning(
                "{Setting} is not set. Scenario results are not published.",
                $"{TestRailOptions.SectionName}:ScenarioSuiteId");
        }

        if (!options.HasApiHealthSuite)
        {
            logger.LogWarning(
                "{Setting} is not set. API Health results are not published.",
                $"{TestRailOptions.SectionName}:ApiHealthSuiteId");
        }
    }

    internal static string Describe(int id, string name)
    {
        if (id > 0)
            return id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(name))
            return $"\"{name}\" (looked up by name)";
        return "not set";
    }
}
