using MongoDB.Bson;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// MongoDB document for the <c>automation_imported_bundles</c> collection.
///
/// <para>
/// One document per <i>distinct</i> imported FHIR transaction bundle. Bundles are
/// content-addressed by <see cref="ContentHash"/> (SHA-256 hex of the raw JSON), so the
/// same patient bundle uploaded into multiple scenarios &mdash; or imported repeatedly
/// over the lifetime of a single scenario &mdash; is stored exactly once. The set of
/// scenarios currently using a bundle is tracked in <see cref="ScenarioIds"/>; pruning
/// only deletes the document once <i>every</i> referencing scenario has either dropped
/// the bundle or been deleted (and only then if <see cref="IsLibraryEntry"/> is false).
/// </para>
///
/// <para>
/// This shape also forms the storage substrate for a future patient-bundle library UI:
/// <list type="bullet">
///   <item><see cref="Name"/> / <see cref="Description"/> / <see cref="Tags"/> &mdash;
///         human-curated metadata for browsing and filtering.</item>
///   <item><see cref="IsLibraryEntry"/> &mdash; pins a bundle so it survives even when
///         no scenario references it (e.g., a curated reference-patient catalog).</item>
///   <item><see cref="PatientId"/> / <see cref="FileName"/> / <see cref="ByteCount"/>
///         &mdash; surface fields for list views without having to deserialize the
///         full bundle JSON.</item>
/// </list>
/// The MongoScenarioStore save/load paths populate the operational fields automatically;
/// the rest are placeholders the future library service can fill in.
/// </para>
/// </summary>
[BsonIgnoreExtraElements]
public sealed class ImportedBundleDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    /// <summary>
    /// SHA-256 hex of the bundle JSON. Unique across the collection; used for the
    /// "find or insert" upsert that powers cross-scenario deduplication.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// Scenarios currently referencing this bundle. Maintained via <c>$addToSet</c> on
    /// scenario upsert and <c>$pull</c> on scenario delete / bundle removal.
    /// </summary>
    [BsonRepresentation(BsonType.String)]
    public List<Guid> ScenarioIds { get; set; } = [];

    /// <summary>FHIR Patient.id this bundle represents (display + lookup convenience).</summary>
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Original upload filename, when known. Display only.</summary>
    public string? FileName { get; set; }

    /// <summary>Raw FHIR transaction bundle JSON.</summary>
    public string BundleJson { get; set; } = string.Empty;

    /// <summary>Cached UTF-8 byte length of <see cref="BundleJson"/> for list views.</summary>
    public long ByteCount { get; set; }

    /// <summary>
    /// Human-curated display name (future patient-library UI). Defaults to the upload
    /// filename or <see cref="PatientId"/> when unset.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>Human-curated description (future patient-library UI).</summary>
    public string? Description { get; set; }

    /// <summary>Human-curated tags (future patient-library UI; faceted filtering).</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// When true the bundle is preserved even after the last scenario reference is
    /// dropped (curated library entries). The store never sets this on its own; it is
    /// reserved for a future library-management surface.
    /// </summary>
    public bool IsLibraryEntry { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Lightweight reference embedded on <see cref="TestScenarioDocument"/> linking each
/// imported-bundle <see cref="ImportedPatientInput"/> back to its row in the
/// <c>automation_imported_bundles</c> collection. The same <see cref="BundleId"/> may
/// be referenced by many scenarios &mdash; bundles are content-addressed and shared.
/// </summary>
public sealed class ImportedBundleReference
{
    [BsonRepresentation(BsonType.String)]
    public Guid BundleId { get; set; }

    /// <summary>FHIR Patient.id used to re-attach the hydrated bundle to the right input.</summary>
    public string PatientId { get; set; } = string.Empty;
}

