using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Automation.UI.Services.Persistence;

/// <summary>MongoDB document for automation_runs collection.</summary>
[BsonIgnoreExtraElements]
public sealed class AutomationRunDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid RunId { get; set; }

    public string FacilityId { get; set; } = string.Empty;
    /// <summary>True when this run created <see cref="FacilityId"/> rather than reusing a tenant.</summary>
    public bool AutomationCreatedFacility { get; set; }
    public string ReportId { get; set; } = string.Empty;

    public string RunName { get; set; } = string.Empty;
    public string Scenario { get; set; } = string.Empty;
    public string SelectedMeasure { get; set; } = string.Empty;
    public int PatientCount { get; set; }
    public int ResourcesPerPatient { get; set; }
    public int Seed { get; set; }
    public bool IsMetricsRun { get; set; }
    public string? RunConfigurationJson { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }

    // Store DateTimeOffset values as native BSON ISODate (UTC) so server-side range
    // queries and indexes work. The driver's default representation is a two-element
    // array [ticks, offsetMinutes], which is not indexable and makes $gte/$lte fall
    // into Mongo's per-element array match semantics (silently dropping docs).
    //
    // Reads remain compatible with legacy array-form documents because the driver's
    // DateTimeOffsetSerializer is polymorphic on the read path (Array, DateTime,
    // Document, and String representations all deserialize correctly). New writes
    // use ISODate because of the attribute below.
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset StartedAt { get; set; }
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? FinishedAt { get; set; }
    /// <summary>Human-readable pipeline duration (report created ? submitted). Populated at run completion.</summary>
    public string? Duration { get; set; }
    [BsonRepresentation(BsonType.String)]
    public Guid? GeneratedTemplateCacheVersionId { get; set; }
    public int? GeneratedTemplateCacheVersionNumber { get; set; }
    public string? GeneratedTemplateCacheScenarioKey { get; set; }
    public string? GeneratedTemplateSetHash { get; set; }
}

/// <summary>Facility Automation created whose run summary was deleted before teardown.</summary>
public sealed class OwnedFacilityTombstoneDocument
{
    [BsonId]
    public string FacilityId { get; set; } = string.Empty;

    public string RunId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The deleted run's cleanup timestamp. Teardown waits until this is older than retention.</summary>
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset EligibleAt { get; set; }
}

/// <summary>Facility id already torn down for a run whose snapshot is still waiting to be purged.</summary>
public sealed class FacilityTeardownProgressDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid RunId { get; set; }

    public string FacilityId { get; set; } = string.Empty;
}

/// <summary>MongoDB document for automation_run_inputs collection.</summary>
public sealed class AutomationRunInputDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid RunId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? ScenarioId { get; set; }

    public string? ScenarioName { get; set; }
    public string? RunConfigurationJson { get; set; }

    [BsonRepresentation(BsonType.String)]
    public List<Guid> ImportedBundleIds { get; set; } = [];

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset CreatedAt { get; set; }

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>MongoDB document for automation_run_snapshots collection (one per run+domain).</summary>
[BsonIgnoreExtraElements]
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

/// <summary>
/// MongoDB document for an ordered chunk in automation_run_logs.
/// Extra elements are ignored so a newer field does not break this build.
/// An older build without that attribute still fails to read a chunk this build has written.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class RunLogDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public Guid RunId { get; set; }

    public int ChunkNumber { get; set; }
    public int LineCount { get; set; }
    public int BsonByteCount { get; set; }
    public List<string> Lines { get; set; } = [];
    public List<long> LineSequences { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Chunk number where a split of this document started writing replacements.
    /// Set before the first replacement insert so a retry reuses the same ids.
    /// </summary>
    public int? SplitStart { get; set; }

    /// <summary>Id of the source chunk this replacement was split from.</summary>
    public string? SplitFromId { get; set; }

    /// <summary>How many replacement chunks <see cref="SplitStart"/> reserves.</summary>
    public int? SplitCount { get; set; }

    /// <summary>
    /// Process that owns an in-progress split. Another process takes over only
    /// after <see cref="SplitClaimedAt"/> is older than the claim lease.
    /// </summary>
    public string? SplitOwner { get; set; }

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? SplitClaimedAt { get; set; }

    /// <summary>
    /// 1 when <see cref="BsonByteCount"/> counts escaped JSON bytes.
    /// A missing or zero value is a legacy raw UTF-8 count and is recomputed before another line is appended.
    /// </summary>
    public int ByteCountVersion { get; set; }
}

public sealed class RunLogSequenceDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid RunId { get; set; }

    public long NextSequence { get; set; }
}

/// <summary>
/// Lease for a log split whose source chunk cannot accept more fields without
/// crossing the Cosmos 2 MB document cap. The source document is left unchanged.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class LogSplitClaimDocument
{
    public const string CollectionName = "automation_log_split_claims";

    [BsonId]
    public string Id { get; set; } = string.Empty;

    public string Owner { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset ClaimedAt { get; set; }

    public int SplitStart { get; set; }

    public int SplitCount { get; set; }
}

/// <summary>
/// Line sequences for a log chunk that cannot store them without crossing the
/// Cosmos 2 MB document cap. The chunk document is left unchanged.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class LogSequenceStampDocument
{
    public const string CollectionName = "automation_log_sequence_stamps";

    [BsonId]
    public string Id { get; set; } = string.Empty;

    public List<long> LineSequences { get; set; } = [];
}

/// <summary>
/// One piece of a domain snapshot. The id includes the generation so a rewrite
/// does not overwrite the generation a reader is still using.
/// <see cref="GenerationId"/> must match the header before a reader accepts the piece.
/// </summary>
/// <summary>
/// A committed generation whose parts still need to be removed. The sweep
/// retries these after a writer crashes or a delete fails. A generation that
/// is still the header is left alone.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class RetiredSnapshotGenerationDocument
{
    public const string CollectionName = "automation_snapshot_retired_generations";

    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public Guid RunId { get; set; }

    public string Domain { get; set; } = string.Empty;

    public string GenerationId { get; set; } = string.Empty;
}

[BsonIgnoreExtraElements]
public sealed class SnapshotPartDocument
{
    public const string CollectionName = "automation_snapshot_parts";

    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public Guid RunId { get; set; }

    public string Domain { get; set; } = string.Empty;

    public string GenerationId { get; set; } = string.Empty;

    public int Ordinal { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public int Index { get; set; }

    public int Slice { get; set; }

    public string? ItemKey { get; set; }

    public string Data { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// True once this part belongs to the committed header. The orphan sweep
    /// skips settled parts so it does not reread every retained generation.
    /// </summary>
    public bool Settled { get; set; }
}
