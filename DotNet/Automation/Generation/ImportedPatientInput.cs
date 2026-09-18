namespace LantanaGroup.Automation.Generation;

using Hl7.Fhir.Model;
using System.Text.Json.Serialization;

/// <summary>
/// Source of an imported patient.
/// </summary>
public enum ImportedPatientSource
{
    /// <summary>Patient already exists on the FHIR server; data is fetched at run time.</summary>
    ExistingId,

    /// <summary>Patient data is supplied as a transaction bundle and uploaded to the FHIR server during the run.</summary>
    Bundle
}

/// <summary>
/// Describes a single imported patient as configured on a scenario.
/// One of <see cref="PatientId"/> (for <see cref="ImportedPatientSource.ExistingId"/>)
/// or <see cref="UploadedBundleId"/> (for <see cref="ImportedPatientSource.Bundle"/>) is required.
/// Raw <see cref="BundleJson"/> is an execution-only value populated after resolving the
/// external bundle reference, with support retained for legacy inline inputs.
/// </summary>
public sealed class ImportedPatientInput
{
    /// <summary>How this patient's data is sourced.</summary>
    public ImportedPatientSource Source { get; set; } = ImportedPatientSource.ExistingId;

    /// <summary>FHIR Patient.id. Required for both source kinds.</summary>
    public string PatientId { get; set; } = string.Empty;

    /// <summary>For <see cref="ImportedPatientSource.Bundle"/>: the original file name (display only).</summary>
    public string? FileName { get; set; }

    /// <summary>
    /// For <see cref="ImportedPatientSource.Bundle"/>: identifier of an uploaded bundle
    /// stored in Automation.UI persistence. When set, saves can reference the uploaded
    /// payload without reposting <see cref="BundleJson"/>.
    /// </summary>
    public Guid? UploadedBundleId { get; set; }

    /// <summary>
    /// For <see cref="ImportedPatientSource.Bundle"/>: the raw FHIR Bundle JSON available
    /// only to execution-time processing. New persisted models use <see cref="UploadedBundleId"/>
    /// instead; this remains for legacy inline inputs and hydrated execution copies.
    /// </summary>
    public string? BundleJson { get; set; }

    /// <summary>
    /// When true, run the classifier on the patient's resources to derive
    /// <see cref="MeasureEligibilities"/>. Always true for new UI saves; kept for
    /// older payloads.
    /// </summary>
    public bool AutoDetect { get; set; } = true;

    /// <summary>
    /// Derived per-measure IP prediction from the imported resources. Display snapshot
    /// only; the pipeline re-classifies from the bundle at run time.
    /// </summary>
    public Dictionary<ProfiledMeasureType, MeasureEligibility> MeasureEligibilities { get; set; } = [];

    /// <summary>
    /// Optional clinical scenario ID detected from the patient's data. Informational only.
    /// </summary>
    public string? DetectedClinicalScenarioId { get; set; }

    /// <summary>
    /// Transient: FHIR bundle entries loaded by <see cref="ImportedPatientLoader"/>
    /// during run pre-flight (so the pipeline can reuse them and the runner can compute
    /// a clinical period that encompasses the patient's actual encounter dates).
    /// Excluded from JSON / Mongo serialization.
    /// </summary>
    [JsonIgnore]
    public List<Bundle.EntryComponent>? PreLoadedEntries { get; set; }
}
