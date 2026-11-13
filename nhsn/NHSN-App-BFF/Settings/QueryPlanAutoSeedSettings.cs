namespace LantanaGroup.Link.Nhsn.App.Bff.Settings;

// Auto-seeding a facility's DataAcquisition QueryPlan from a per-vendor static template on
// Facility Information save. Off by default -- a real production facility may already carry a
// hand-configured plan that must never be touched, so this only goes on in lower environments
// until the team is ready to design provenance tracking for production use.
public class QueryPlanAutoSeedSettings
{
    public const string SectionName = "QueryPlanAutoSeed";

    public bool Enabled { get; set; } = false;
}
