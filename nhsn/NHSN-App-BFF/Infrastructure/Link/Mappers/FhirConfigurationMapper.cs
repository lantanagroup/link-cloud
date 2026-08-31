using System.Text.Json.Serialization;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.Mappers;

// Data Acquisition's FHIR query configuration as it appears on the wire, and its mapping to
// FhirSection.
//
// Declared locally because GetFhirQueryConfigurationAsync returns the non-generic LinkApiResponse
// — no Body, only RawBody — so the adapter deserializes it itself. Delete this type once the SDK
// types that method.
//
// TimeZone is absent on purpose. Data Acquisition doesn't store it — it's an input-only field used
// once to convert the pull times to UTC before persisting, and the read returns times already in
// UTC. Carrying it into a later write would convert already-UTC values a second time, walking the
// pull window by the UTC offset on every save, silently.
internal sealed record DataAcqFhirConfiguration
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("facilityId")]
    public string? FacilityId { get; init; }

    [JsonPropertyName("fhirServerBaseUrl")]
    public string? FhirServerBaseUrl { get; init; }

    [JsonPropertyName("maxConcurrentRequests")]
    public int? MaxConcurrentRequests { get; init; }

    [JsonPropertyName("maxRetries")]
    public int? MaxRetries { get; init; }

    [JsonPropertyName("minAcquisitionPullTime")]
    public string? MinAcquisitionPullTime { get; init; }

    [JsonPropertyName("maxAcquisitionPullTime")]
    public string? MaxAcquisitionPullTime { get; init; }
}

internal static class FhirConfigurationMapper
{
    public static FhirSection ToDomain(DataAcqFhirConfiguration source) => new()
    {
        FhirServerBaseUrl = source.FhirServerBaseUrl,
        MaxConcurrentRequests = source.MaxConcurrentRequests,
        MaxRetries = source.MaxRetries,
        MinAcquisitionPullTime = source.MinAcquisitionPullTime,
        MaxAcquisitionPullTime = source.MaxAcquisitionPullTime
        // ConnectionTested and LagDuration are merged in by the assembler: the first is a UI flag
        // from DraftJson, the second belongs to Query Dispatch.
    };
}
