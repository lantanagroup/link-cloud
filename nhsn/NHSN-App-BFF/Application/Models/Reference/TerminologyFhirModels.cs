namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reference;

// Minimal FHIR JSON shape for the Terminology operation ReferenceDataService calls for the
// Encounter Mapping step's code-detail lookup. ITerminologyServiceClient returns raw JSON strings
// rather than Hl7.Fhir.Model types (see its XML doc), and this BFF has no Hl7.Fhir dependency of
// its own, so only the fields actually read are modeled here.

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
