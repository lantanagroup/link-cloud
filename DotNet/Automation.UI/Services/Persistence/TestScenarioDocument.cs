using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Automation.UI.Services.Persistence;

/// <summary>MongoDB document for automation_scenarios collection.</summary>
/// <remarks>
/// <see cref="BsonIgnoreExtraElementsAttribute"/> protects readers from older documents
/// written before deprecated fields (UseMeasureEligibilityProfiles, PatientProfilesJson,
/// SelectedClinicalScenarioIds, DischargeCount/QualifyingCount/NonQualifyingCount) were
/// removed; without it the driver throws on any unmapped element.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class TestScenarioDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemScenario { get; set; }

    public string ReportMethod { get; set; } = "Adhoc";
    public List<string> SelectedMeasures { get; set; } = [];

    public int Seed { get; set; }
    public int PatientCount { get; set; }
    public int ResourcesPerPatientMin { get; set; }
    public int ResourcesPerPatientMax { get; set; }

    /// <summary>Serialized cohort configuration as JSON string.</summary>
    public string PatientCohortsJson { get; set; } = "[]";

    [BsonRepresentation(BsonType.String)]
    public Guid? QueryPlanTemplateId { get; set; }

    public bool CleanupServiceData { get; set; }
    public bool CleanupFhirData { get; set; } = true;

    /// <summary>Optional reporting period start (UTC).</summary>
    public DateTime? ReportPeriodStart { get; set; }

    /// <summary>Optional reporting period end (UTC).</summary>
    public DateTime? ReportPeriodEnd { get; set; }

    /// <summary>Serialized List&lt;ImportedPatientInput&gt; for ID-based imported patients.</summary>
    public string ImportedPatientIdsJson { get; set; } = "[]";

    /// <summary>Serialized List&lt;ImportedPatientInput&gt; for bundle-based imported patients.</summary>
    public string ImportedPatientBundlesJson { get; set; } = "[]";

    public DateTimeOffset UpdatedAt { get; set; }
}
