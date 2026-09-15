namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reference;

// Minimal FHIR JSON shapes for the two Terminology operations ReferenceDataService calls for the
// Encounter Mapping step. ITerminologyServiceClient returns raw JSON strings rather than
// Hl7.Fhir.Model types (see its XML doc), and this BFF has no Hl7.Fhir dependency of its own, so
// only the fields actually read are modeled here.

// ValueSet/$expand response — GetEncounterCodesAsync reads expansion.contains[].
public sealed class ValueSetJson
{
    public ValueSetExpansionJson? Expansion { get; set; }
}

public sealed class ValueSetExpansionJson
{
    public List<ValueSetContainsJson> Contains { get; set; } = [];
}

public sealed class ValueSetContainsJson
{
    public string? System { get; set; }
    public string? Code { get; set; }
    public string? Display { get; set; }
}

// CodeSystem/$lookup response — LookupEncounterCodeAsync reads the name/version/display
// parameters FhirService.LookupCodeInCodeSystem returns.
public sealed class ParametersJson
{
    public List<ParameterJson> Parameter { get; set; } = [];
}

public sealed class ParameterJson
{
    public string? Name { get; set; }
    public string? ValueString { get; set; }
}
