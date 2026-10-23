namespace LantanaGroup.Link.PatientFlowSimulator.Scenario;

/// <summary>
/// A coded value from the scenario file.
/// </summary>
public sealed class CodeSpec
{
    /// <summary>
    /// Code system URI.
    /// </summary>
    public string System { get; set; } = "";

    /// <summary>
    /// The code.
    /// </summary>
    public string Code { get; set; } = "";

    /// <summary>
    /// Human-readable display.
    /// </summary>
    public string Display { get; set; } = "";
}
