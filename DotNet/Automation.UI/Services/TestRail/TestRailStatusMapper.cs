using LantanaGroup.Link.Automation.Link.Models;

namespace Automation.UI.Services.TestRail;

public static class TestRailStatusMapper
{
    public const int Passed = 1;
    public const int Blocked = 2;
    public const int Failed = 5;

    public static int FromScenarioStatus(AutomationRunStatus status) => status switch
    {
        AutomationRunStatus.Succeeded => Passed,
        AutomationRunStatus.Failed => Failed,
        AutomationRunStatus.Cancelled => Blocked,
        _ => Failed
    };

    public static int? FromApiHealthResult(bool passed, bool skipped, int skipStatusId)
    {
        if (skipped)
            return skipStatusId > 0 ? skipStatusId : null;

        return passed ? Passed : Failed;
    }
}
