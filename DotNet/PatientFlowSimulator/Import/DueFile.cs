namespace LantanaGroup.Link.PatientFlowSimulator.Import;

/// <summary>
/// A generated hourly file whose hour has elapsed and that has not been uploaded yet.
/// </summary>
public sealed record DueFile(DateTimeOffset Hour, string Path)
{
    /// <summary>
    /// Marker written next to the file once it has been uploaded.
    /// </summary>
    public string DoneMarkerPath => Path + ".done";
}
