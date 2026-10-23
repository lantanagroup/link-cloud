namespace LantanaGroup.Link.PatientFlowSimulator.Generation;

/// <summary>
/// One facility's month as recorded in the manifest: what Link should be configured with, what was
/// generated, and how many patients are expected in each measure.
/// </summary>
public sealed class FacilityManifest
{
    public string FacilityId { get; set; } = "";
    public string Name { get; set; } = "";
    public string TimeZone { get; set; } = "";
    public string MonthStartUtc { get; set; } = "";
    public string MonthEndUtc { get; set; } = "";
    public List<string> Measures { get; set; } = [];
    public List<string> CensusListIds { get; set; } = [];
    public int PatientsAdmittedThisMonth { get; set; }
    public int PatientsCarriedOver { get; set; }

    /// <summary>
    /// Expected qualifying patients per measure, counting only patients admitted this month.
    /// </summary>
    public Dictionary<string, int> ExpectedQualifying { get; set; } = new();

    /// <summary>
    /// Expected qualifying patients per measure, also counting carried-over patients whose stay overlaps
    /// this month.
    /// </summary>
    public Dictionary<string, int> ExpectedQualifyingWithCarryOvers { get; set; } = new();

    public List<PatientManifest> Patients { get; set; } = [];
    public List<PatientManifest> CarryOvers { get; set; } = [];
}
