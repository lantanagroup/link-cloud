namespace LantanaGroup.Link.PatientFlowSimulator.Import;

/// <summary>
/// What one import run did.
/// </summary>
public sealed class ImportResult
{
    /// <summary>
    /// Months generated because they did not exist yet.
    /// </summary>
    public List<string> GeneratedMonths { get; } = [];

    /// <summary>
    /// Files uploaded (or, in a dry run, that would have been).
    /// </summary>
    public int Imported { get; set; }

    /// <summary>
    /// Files that were due but not uploaded because an earlier one failed.
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>
    /// Path of the file whose upload failed, if any.
    /// </summary>
    public string? FailedFile { get; set; }

    /// <summary>
    /// Why the upload failed, if it did.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// True when nothing failed.
    /// </summary>
    public bool Succeeded => FailedFile == null;
}
