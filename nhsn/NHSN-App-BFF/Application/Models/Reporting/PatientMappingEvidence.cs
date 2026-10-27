namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

// The real evidence behind a patient's Location Org / HSLOC / Encounter mapping indicators --
// what Acquisition/Normalization actually found, projected from Report's per-patient detail
// operation (GetEntryByScheduleAndPatientAsync). Null sections mean that source has not reported.
public sealed record PatientMappingEvidence
{
    public LocationOrgEvidence? LocationOrg { get; init; }

    public IReadOnlyList<CodeMapEvidence> CodeMaps { get; init; } = [];
}

public sealed record LocationOrgEvidence
{
    public int EncounterCount { get; init; }

    public int OrgEncounterCount { get; init; }

    public int AssumedOrgEncounterCount { get; init; }

    public IReadOnlyList<LocationOrgMatch> Matches { get; init; } = [];
}

public sealed record LocationOrgMatch
{
    public required string LocationId { get; init; }

    public string? LocationName { get; init; }

    public string? LocationAlias { get; init; }

    public string? PartOfValue { get; init; }

    public bool IsOrgLocation { get; init; }
}

public sealed record CodeMapEvidence
{
    public required string SourceSystem { get; init; }

    public required string TargetSystem { get; init; }

    public int MappedCount { get; init; }

    public int UnmappedCount { get; init; }

    public int FailureCount { get; init; }

    public IReadOnlyList<string> UnmappedCodes { get; init; } = [];
}
