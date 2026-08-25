namespace Automation.UI.Services.TestRail;

public sealed class TestRailOptions
{
    public const string SectionName = "Automation:TestRail";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public int ProjectId { get; set; }

    public int ScenarioSuiteId { get; set; }

    public int ApiHealthSuiteId { get; set; }

    public bool UseSharedRun { get; set; }

    public int? SharedScenarioRunId { get; set; }

    public int? SharedApiHealthRunId { get; set; }

    public int SkipStatusId { get; set; }

    public Dictionary<string, int> ScenarioCaseIds { get; set; } = new();

    public Dictionary<string, int> ApiHealthCaseIds { get; set; } = new();

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(Username)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && ProjectId > 0;
}
