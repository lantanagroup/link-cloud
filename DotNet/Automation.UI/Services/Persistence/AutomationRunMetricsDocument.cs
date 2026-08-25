using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Automation.UI.Services.Persistence;

[BsonIgnoreExtraElements]
public sealed class AutomationRunMetricsDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid RunId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? ScenarioId { get; set; }

    public string ScenarioName { get; set; } = string.Empty;
    public string? BenchmarkKey { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset StartedAt { get; set; }

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset FinishedAt { get; set; }

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset CreatedAt { get; set; }

    public string Outcome { get; set; } = string.Empty;
    public int PatientCount { get; set; }
    public int ResourcesPerPatientMin { get; set; }
    public int ResourcesPerPatientMax { get; set; }
    public ThetisRevisionSnapshot Thetis { get; set; } = new();
    public long PrometheusWaitMs { get; set; }
    public Dictionary<string, StageLatencySnapshot> Stages { get; set; } = new(StringComparer.Ordinal);
    public ThroughputSnapshot Throughput { get; set; } = new();
    public double E2eDurationSeconds { get; set; }
    public List<ValidatorOutcomeSnapshot> Validators { get; set; } = [];
}

public sealed class ThetisRevisionSnapshot
{
    public string Generator { get; set; } = "thetis";
    public string Source { get; set; } = "sibling-project-ref";
    public string? GitSha { get; set; }
    public string? AssemblyInformationalVersion { get; set; }
    public int Seed { get; set; }
    public long DurationMs { get; set; }
}

public sealed class StageLatencySnapshot
{
    public bool Unavailable { get; set; } = true;
    public double Count { get; set; }
    public double P50Ms { get; set; }
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
    public double ErrorCount { get; set; }
}

public sealed class ThroughputSnapshot
{
    public double PatientsPerMinute { get; set; }
    public double ResourcesPerSecond { get; set; }
}

public sealed class ValidatorOutcomeSnapshot
{
    public string Name { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public int IssueCount { get; set; }
}
