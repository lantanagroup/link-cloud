namespace LantanaGroup.Link.DataAcquisition.Domain.Settings;

public class TailMessageRecoveryJobSettings
{
    public const string SectionName = "TailMessageRecoveryJobSettings";

    // Run infrequently by default (~every 10 minutes)
    public string CronSchedule { get; set; } = "0 */10 * * * ?";

    // Only consider groups that have been quiet for this long
    public int MinAgeMinutes { get; set; } = 15;

    // Keep each run lightweight
    public int MaxGroupsPerRun { get; set; } = 25;

    // Hard stop to avoid competing with primary ingestion work
    public int TimeBudgetPerRunSeconds { get; set; } = 20;
}
