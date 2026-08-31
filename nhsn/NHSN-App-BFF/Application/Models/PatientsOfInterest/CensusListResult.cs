namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

// listKey is a plain string, not an enum - the UI's six keys are kebab-case literals
// ('admit-lt-24' etc.), which a C# enum cannot round-trip through JsonStringEnumConverter
// without also renaming every other enum's wire format.
public class CensusListResult
{
    public required string ListKey { get; set; }
    public required int PatientCount { get; set; }
    public required IReadOnlyList<string> PatientIds { get; set; }
    public bool Simulated { get; set; }
}

public class PatientListQueryRequest
{
    public required string ListKey { get; set; }
}
