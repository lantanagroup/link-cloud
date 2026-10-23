namespace LantanaGroup.Link.PatientFlowSimulator.Planning;

/// <summary>
/// The things that happen during a stay, each of which lands in the hourly file of the hour it happens in.
/// </summary>
public enum StayEventKind
{
    Admit,
    Observation,
    MedicationAdministration,
    Discharge
}
