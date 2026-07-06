namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Defines when a patient's inpatient stay starts/ends relative to the scheduled report period.
/// Used by scheduled-report automation to emit the right admit/discharge events.
/// </summary>
public enum ScheduledInpatientPattern
{
    AdmittedBeforePeriodRemainsInpatientAfterPeriod,
    AdmittedBeforePeriodDischargedDuringPeriod,
    AdmittedDuringPeriodRemainsInpatientAfterPeriod,
    AdmittedDuringPeriodDischargedDuringPeriod,
    AdmittedAndDischargedBeforePeriod,
    AdmittedAndDischargedAfterPeriod
}

/// <summary>
/// Describes the census-event orchestration a scenario runner must perform to exercise a
/// <see cref="ScheduledInpatientPattern"/>, plus whether the patient is expected to appear
/// in the finalized report.
/// <para>
/// In census terms the "admitted before" vs "admitted during" distinction is irrelevant —
/// that timing is expressed only through the synthetic FHIR encounter dates (which drive
/// measure evaluation). What the census flow actually needs to know is: should the patient
/// be admitted inside the active window, should they be discharged inside the window (which
/// triggers a QueryDispatch discharge dispatch), and should they end up in the report.
/// </para>
/// </summary>
/// <param name="EmitAdmitDuringWindow">
/// When true the scenario runner admits the patient inside the active report window (via a
/// <c>PatientListsAcquired</c> snapshot) so a report entry is created. Patterns whose entire
/// clinical stay sits outside the report period emit no census events and rely on the report
/// period boundaries / measure-eval to exclude them.
/// </param>
/// <param name="EmitDischargeDuringWindow">
/// When true the patient is discharged inside the report window, triggering a QueryDispatch
/// discharge dispatch (and therefore data acquisition) during the run. When false the patient
/// remains inpatient and is captured by the end-of-report-period job.
/// </param>
/// <param name="ExpectedInReport">
/// True when a qualifying patient with this pattern should appear in the submitted report.
/// </param>
public readonly record struct ScheduledPatternCensusBehavior(
    bool EmitAdmitDuringWindow,
    bool EmitDischargeDuringWindow,
    bool ExpectedInReport);

/// <summary>
/// Extension helpers that map each <see cref="ScheduledInpatientPattern"/> to the concrete
/// census behavior the scenario runner must execute. This is the single source of truth
/// shared by orchestration (which events to emit) and validation (who should be in the
/// report), so the two can never drift.
/// </summary>
public static class ScheduledInpatientPatternExtensions
{
    /// <summary>
    /// Returns the census-event behavior required to exercise the given pattern.
    /// </summary>
    public static ScheduledPatternCensusBehavior GetCensusBehavior(this ScheduledInpatientPattern pattern) => pattern switch
    {
        // Clinically admitted before the period and still inpatient when it ends: admit inside
        // the window and never discharge — the end-of-report-period job acquires the patient.
        ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod
            => new ScheduledPatternCensusBehavior(EmitAdmitDuringWindow: true, EmitDischargeDuringWindow: false, ExpectedInReport: true),

        // Admitted before, discharged during the period: admit then discharge inside the window;
        // the discharge dispatch drives acquisition.
        ScheduledInpatientPattern.AdmittedBeforePeriodDischargedDuringPeriod
            => new ScheduledPatternCensusBehavior(EmitAdmitDuringWindow: true, EmitDischargeDuringWindow: true, ExpectedInReport: true),

        // Admitted during, remains inpatient after: admit inside the window, never discharge.
        ScheduledInpatientPattern.AdmittedDuringPeriodRemainsInpatientAfterPeriod
            => new ScheduledPatternCensusBehavior(EmitAdmitDuringWindow: true, EmitDischargeDuringWindow: false, ExpectedInReport: true),

        // Admitted during, discharged during: admit then discharge inside the window.
        ScheduledInpatientPattern.AdmittedDuringPeriodDischargedDuringPeriod
            => new ScheduledPatternCensusBehavior(EmitAdmitDuringWindow: true, EmitDischargeDuringWindow: true, ExpectedInReport: true),

        // Entire stay before the period: no census events during the window; excluded from report.
        ScheduledInpatientPattern.AdmittedAndDischargedBeforePeriod
            => new ScheduledPatternCensusBehavior(EmitAdmitDuringWindow: false, EmitDischargeDuringWindow: false, ExpectedInReport: false),

        // Entire stay after the period: no census events during the window; excluded from report.
        ScheduledInpatientPattern.AdmittedAndDischargedAfterPeriod
            => new ScheduledPatternCensusBehavior(EmitAdmitDuringWindow: false, EmitDischargeDuringWindow: false, ExpectedInReport: false),

        _ => new ScheduledPatternCensusBehavior(EmitAdmitDuringWindow: true, EmitDischargeDuringWindow: true, ExpectedInReport: true)
    };
}
