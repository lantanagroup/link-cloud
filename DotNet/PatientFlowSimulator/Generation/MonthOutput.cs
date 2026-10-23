using LantanaGroup.Link.PatientFlowSimulator.Planning;

namespace LantanaGroup.Link.PatientFlowSimulator.Generation;

/// <summary>
/// A generated month: its hourly files and its manifest.
/// </summary>
public sealed class MonthOutput
{
    public MonthOutput(MonthPlan plan, IReadOnlyList<HourlyFile> files, MonthManifest manifest)
    {
        Plan = plan;
        Files = files;
        Manifest = manifest;
    }

    /// <summary>
    /// The month.
    /// </summary>
    public YearMonth Month => Plan.Month;

    /// <summary>
    /// The stays the files were generated from.
    /// </summary>
    public MonthPlan Plan { get; }

    /// <summary>
    /// Hourly files in time order. Hours with nothing to upload have no file.
    /// </summary>
    public IReadOnlyList<HourlyFile> Files { get; }

    /// <summary>
    /// The manifest.
    /// </summary>
    public MonthManifest Manifest { get; }
}
