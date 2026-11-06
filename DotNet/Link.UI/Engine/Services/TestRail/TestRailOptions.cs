namespace Automation.UI.Services.TestRail;

/// <summary>
/// TestRail publishing settings, read from the <c>Automation:TestRail</c> section by
/// <see cref="TestRailOptionsBinder"/>. Values that cannot be read leave publishing off and are
/// listed in <see cref="ConfigurationProblems"/>; they never stop the service from starting.
/// </summary>
public sealed class TestRailOptions
{
    public const string SectionName = "Automation:TestRail";

    public const int DefaultRequestTimeoutSeconds = 15;

    public bool Enabled { get; set; }

    /// <summary>Site root, for example https://example.testrail.io. Any API suffix is removed when read.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Email address of the TestRail user that owns <see cref="ApiKey"/>.</summary>
    public string Username { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Numeric project id. Zero when the project is configured by name.</summary>
    public int ProjectId { get; set; }

    /// <summary>Project name, used when the ProjectId setting holds a name instead of a number.</summary>
    public string ProjectName { get; set; } = string.Empty;

    public int ScenarioSuiteId { get; set; }

    /// <summary>Suite name, used when the ScenarioSuiteId setting holds a name instead of a number.</summary>
    public string ScenarioSuiteName { get; set; } = string.Empty;

    public int ApiHealthSuiteId { get; set; }

    /// <summary>Suite name, used when the ApiHealthSuiteId setting holds a name instead of a number.</summary>
    public string ApiHealthSuiteName { get; set; } = string.Empty;

    /// <summary>
    /// Per-call HTTP timeout for the TestRail client. Values below 1 use <see cref="DefaultRequestTimeoutSeconds"/>.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = DefaultRequestTimeoutSeconds;

    public bool UseSharedRun { get; set; }

    public int? SharedScenarioRunId { get; set; }

    public int? SharedApiHealthRunId { get; set; }

    public Dictionary<string, int> ScenarioCaseIds { get; set; } = new();

    public Dictionary<string, int> ApiHealthCaseIds { get; set; } = new();

    /// <summary>Values that could not be read. Never contains the API key.</summary>
    public List<string> ConfigurationProblems { get; } = [];

    public bool HasProject => ProjectId > 0 || !string.IsNullOrWhiteSpace(ProjectName);

    public bool HasScenarioSuite => ScenarioSuiteId > 0 || !string.IsNullOrWhiteSpace(ScenarioSuiteName);

    public bool HasApiHealthSuite => ApiHealthSuiteId > 0 || !string.IsNullOrWhiteSpace(ApiHealthSuiteName);

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(Username)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && HasProject;

    /// <summary>Names of the settings that must be set before publishing turns on.</summary>
    public IReadOnlyList<string> MissingRequiredSettings()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(BaseUrl))
            missing.Add("BaseUrl");
        if (string.IsNullOrWhiteSpace(Username))
            missing.Add("Username");
        if (string.IsNullOrWhiteSpace(ApiKey))
            missing.Add("ApiKey");
        if (!HasProject)
            missing.Add("ProjectId");
        return missing;
    }
}
