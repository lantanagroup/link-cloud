using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Automation.UI.Services.Persistence;

/// <summary>MongoDB document for automation_scenarios collection.</summary>
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
    public string PatientPrefix { get; set; } = string.Empty;

    public bool UseMeasureEligibilityProfiles { get; set; }

    /// <summary>Serialized as JSON string for flexibility.</summary>
    public string PatientProfilesJson { get; set; } = "[]";

    /// <summary>Serialized cohort configuration as JSON string.</summary>
    public string PatientCohortsJson { get; set; } = "[]";

    public List<string> SelectedClinicalScenarioIds { get; set; } = [];

    public int DischargeCount { get; set; }
    public int DischargeQualifyingCount { get; set; }
    public int DischargeNonQualifyingCount { get; set; }

    public Guid? QueryPlanTemplateId { get; set; }

    public bool CleanupServiceData { get; set; }
    public bool CleanupFhirData { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; }
}
