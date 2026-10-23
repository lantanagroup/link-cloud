namespace LantanaGroup.Link.PatientFlowSimulator.Scenario;

/// <summary>
/// Where the forced cross-month stays land relative to the month boundary.
/// </summary>
public sealed class CrossMonthSettings
{
    /// <summary>
    /// A forced cross-month patient is admitted at most this many hours before the month ends.
    /// </summary>
    public int AdmitWithinHoursOfMonthEnd { get; set; } = 72;

    /// <summary>
    /// A forced cross-month patient is discharged at most this many hours after the next month starts.
    /// </summary>
    public int DischargeWithinHoursOfMonthStart { get; set; } = 72;
}
