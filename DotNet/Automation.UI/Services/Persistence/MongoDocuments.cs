using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Automation.UI.Services.Persistence;

/// <summary>MongoDB document for automation_runs collection.</summary>
public sealed class AutomationRunDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid RunId { get; set; }

    public string FacilityId { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;

    public string RunName { get; set; } = string.Empty;
    public string Scenario { get; set; } = string.Empty;
    public string SelectedMeasure { get; set; } = string.Empty;
    public int PatientCount { get; set; }
    public int ResourcesPerPatient { get; set; }
    public int Seed { get; set; }
    public string? RunConfigurationJson { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }

    // Store DateTimeOffset values as native BSON ISODate (UTC) so server-side range
    // queries and indexes work. The driver's default representation is a two-element
    // array [ticks, offsetMinutes], which is not indexable and makes $gte/$lte fall
    // into Mongo's per-element array match semantics (silently dropping docs).
    //
    // Compatibility notes:
    //   - Reads: the driver's DateTimeOffsetSerializer is polymorphic on the read path
    //     (handles Array, DateTime, Document, and String representations), so legacy
    //     array-form documents still deserialize correctly.
    //   - Writes: all new writes use ISODate because of the attribute below.
    //   - Range queries: NOT transparently compatible. BSON canonical type order
    //     places every Array value below every Date value, so a $gte against an
    //     ISODate excludes legacy array-form docs entirely. A one-time startup
    //     migration in MongoIndexManager.MigrateLegacyRunDateFields rewrites any
    //     pre-existing documents so the 14-day dashboard query sees them again.
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset StartedAt { get; set; }
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? FinishedAt { get; set; }
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? CompletedAt { get; set; }
    /// <summary>Human-readable pipeline duration (report created ? submitted). Populated at run completion.</summary>
    public string? Duration { get; set; }
}

/// <summary>MongoDB document for automation_run_snapshots collection (one per run+domain).</summary>
public sealed class DomainSnapshotDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid RunId { get; set; }

    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Serialized domain payload as plain JSON text.
    /// Using plain JSON avoids Mongo extended-JSON date serialization
    /// surprises during round-trips through System.Text.Json.
    /// </summary>
    public string Data { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>MongoDB document for automation_run_logs collection (one per run).</summary>
public sealed class RunLogDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid RunId { get; set; }

    public List<string> Lines { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; }
}
