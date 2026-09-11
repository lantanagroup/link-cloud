using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reference;

// One selectable time zone.
public sealed record TimezoneResponse
{
    // IANA id, e.g. America/Chicago. This is what Tenant stores.
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    // Used for ordering only; not part of the UI contract.
    [JsonIgnore]
    public TimeSpan BaseUtcOffset { get; init; }
}
