namespace LantanaGroup.Link.Automation.Link.Models;

/// <summary>
/// The NHSN organization id for a run is the facility id for that run.
/// Both are the run id. A scenario does not store one.
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
