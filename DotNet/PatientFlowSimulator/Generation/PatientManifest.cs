namespace LantanaGroup.Link.PatientFlowSimulator.Generation;

/// <summary>
/// One patient as recorded in the month manifest.
/// </summary>
public sealed class PatientManifest
{
    public string PatientId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Gender { get; set; } = "";
    public string BirthDate { get; set; } = "";
    public string Cohort { get; set; } = "";
    public string Template { get; set; } = "";
    public string PlannedInMonth { get; set; } = "";
    public DateTimeOffset AdmitUtc { get; set; }
    public DateTimeOffset DischargeUtc { get; set; }
    public string AdmitLocal { get; set; } = "";
    public string DischargeLocal { get; set; } = "";
    public double LengthOfStayHours { get; set; }
    public bool CrossesMonthBoundary { get; set; }
    public bool ForcedCrossMonth { get; set; }
    public List<string> QualifiesFor { get; set; } = [];
}
