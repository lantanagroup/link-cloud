namespace LantanaGroup.Link.Nhsn.App.Bff.Settings;

// Auto-enrolling a facility in the ACH Monthly reporting plan on the reporting-plan onboarding
// step save. Off by default -- independent of QueryPlanAutoSeedSettings, since these are separate
// capabilities each worth toggling on its own in lower environments before production.
public class MeasureReportingAutoEnrollSettings
{
    public const string SectionName = "MeasureReportingAutoEnroll";

    public bool Enabled { get; set; } = false;
}
