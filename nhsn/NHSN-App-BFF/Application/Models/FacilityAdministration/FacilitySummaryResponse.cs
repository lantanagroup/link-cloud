using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;

// PascalCase is pinned explicitly: this route predates the global camelCase policy and is a
// published integration contract, so its wire shape must not move with that policy.
public class FacilitySummaryResponse
{
    [JsonPropertyName("Id")]
    public Guid Id { get; set; }

    [JsonPropertyName("FacilityId")]
    public string FacilityId { get; set; } = string.Empty;

    [JsonPropertyName("IsOnboarded")]
    public bool IsOnboarded { get; set; }
}
