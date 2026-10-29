namespace LantanaGroup.Link.Automation.Link.Models;

/// <summary>
/// The NHSN organization id shown for a run. It is the facility id once that
/// id is recorded. A summary with no facility id yet shows the run id.
/// </summary>
public static class RunNhsnOrganizationId
{
    public static string For(AutomationRunSummary run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (!string.IsNullOrWhiteSpace(run.FacilityId))
            return run.FacilityId;
        return run.RunId.ToString();
    }
}
