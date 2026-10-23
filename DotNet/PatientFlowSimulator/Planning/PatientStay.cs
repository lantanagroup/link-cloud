using LantanaGroup.Link.PatientFlowSimulator.Scenario;

namespace LantanaGroup.Link.PatientFlowSimulator.Planning;

/// <summary>
/// One planned admission: a patient, the facility and cohort they belong to, and when they are
/// admitted and discharged. Everything generated for the patient derives from this.
/// </summary>
public sealed class PatientStay
{
    /// <summary>
    /// Facility the patient is admitted to.
    /// </summary>
    public required FacilityDefinition Facility { get; init; }

    /// <summary>
    /// Cohort the patient was generated for.
    /// </summary>
    public required CohortDefinition Cohort { get; init; }

    /// <summary>
    /// Clinical template the stay follows.
    /// </summary>
    public required ClinicalTemplate Template { get; init; }

    /// <summary>
    /// Month the patient was admitted in, in the facility's time zone.
    /// </summary>
    public required YearMonth PlanMonth { get; init; }

    /// <summary>
    /// Position inside the cohort for the month.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// Key every random choice for this stay derives from.
    /// </summary>
    public required string Seed { get; init; }

    /// <summary>
    /// The patient.
    /// </summary>
    public required PatientIdentity Identity { get; init; }

    /// <summary>
    /// Admission instant.
    /// </summary>
    public required DateTimeOffset AdmitUtc { get; init; }

    /// <summary>
    /// Discharge instant.
    /// </summary>
    public required DateTimeOffset DischargeUtc { get; init; }

    /// <summary>
    /// True when the stay was deliberately placed across the month boundary.
    /// </summary>
    public required bool ForcedCrossMonth { get; init; }

    /// <summary>
    /// True when the discharge falls in a later month than the admission.
    /// </summary>
    public required bool CrossesMonthBoundary { get; init; }

    /// <summary>
    /// The patient's FHIR id.
    /// </summary>
    public string PatientId => Identity.PatientId;

    /// <summary>
    /// The encounter's FHIR id. One encounter per stay.
    /// </summary>
    public string EncounterId => $"{PatientId}-enc";

    /// <summary>
    /// True when the patient is admitted at the instant.
    /// </summary>
    public bool IsAdmittedAt(DateTimeOffset utc) => AdmitUtc <= utc && utc < DischargeUtc;

    /// <summary>
    /// True when the stay still matters to a month that starts at the given instant: either the patient is
    /// still in, or they were discharged recently enough to be on a discharge list.
    /// </summary>
    public bool ReachesInto(DateTimeOffset monthStartUtc, TimeSpan trailingWindow)
        => AdmitUtc < monthStartUtc && DischargeUtc + trailingWindow > monthStartUtc;
}
