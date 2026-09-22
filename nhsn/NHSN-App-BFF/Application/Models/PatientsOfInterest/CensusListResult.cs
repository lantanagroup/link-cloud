namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

// listKey is a plain string, not an enum - the UI's six keys are kebab-case literals
// ('admit-lt-24' etc.), which a C# enum cannot round-trip through JsonStringEnumConverter
// without also renaming every other enum's wire format.
public class CensusListResult
{
    public required string ListKey { get; set; }
    public required int PatientCount { get; set; }
    public required IReadOnlyList<CensusPatient> Patients { get; set; }
    public bool Simulated { get; set; }
}

// Name is Epic's patient list display name as returned by Data Acquisition - not sourced from a
// FHIR Patient resource, so it may be absent for a list Data Acquisition couldn't resolve a name for.
public class CensusPatient
{
    public required string Id { get; set; }
    public string? Name { get; set; }
}

public class PatientListQueryRequest
{
    public required string ListKey { get; set; }
}
