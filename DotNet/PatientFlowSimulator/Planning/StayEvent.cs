using LantanaGroup.Link.PatientFlowSimulator.Scenario;

namespace LantanaGroup.Link.PatientFlowSimulator.Planning;

/// <summary>
/// One timed thing that happens during a stay.
/// </summary>
/// <param name="WhenUtc">When it happens.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Stay">The stay it belongs to.</param>
/// <param name="Sequence">1-based repeat number for observations and administrations, 0 otherwise.</param>
/// <param name="Observation">The observation spec, for observation events.</param>
/// <param name="Medication">The medication spec, for administration events.</param>
/// <param name="Value">Observation value or administered dose.</param>
public sealed record StayEvent(
    DateTimeOffset WhenUtc,
    StayEventKind Kind,
    PatientStay Stay,
    int Sequence,
    ObservationSpec? Observation,
    MedicationSpec? Medication,
    double Value);
