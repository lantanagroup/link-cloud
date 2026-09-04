namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.OrganizationIdentification;

// One coding of a Location.type CodeableConcept - a real Location commonly carries more than one.
public sealed record LocationTypeCodingResponse
{
    public string? System { get; init; }
    public string? Code { get; init; }
    public string? Display { get; init; }
}

// A candidate FHIR Location. Type fields are populated only for location-type.
public sealed record LocationCandidateResponse
{
    public required string Id { get; init; }
    public required string Display { get; init; }
    public string? TypeText { get; init; }
    public IReadOnlyList<LocationTypeCodingResponse> TypeCodings { get; init; } = [];
}
