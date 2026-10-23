namespace LantanaGroup.Link.PatientFlowSimulator.Scenario;

/// <summary>
/// A ward or unit, typed with an NHSN HSLOC code. The HSLOC code decides whether the measures treat the
/// location as an inpatient, emergency or observation location.
/// </summary>
public sealed class LocationSpec
{
    /// <summary>
    /// Display name of the unit.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// NHSN HSLOC code, e.g. 1060-3 for a medical ward.
    /// </summary>
    public string Hsloc { get; set; } = "";

    /// <summary>
    /// Display text of the HSLOC code.
    /// </summary>
    public string HslocDisplay { get; set; } = "";
}
