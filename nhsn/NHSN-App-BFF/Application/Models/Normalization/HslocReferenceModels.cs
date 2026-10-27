namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Normalization;

// GET /api/normalization/HSLOC row shape. INormalizationServiceClient.GetHslocCodesAsync returns
// the untyped LinkApiResponse (RawBody only, no Body) — this is deserialized from RawBody rather
// than added to LinkSdk, mirroring how NormalizationCodeMapModels' request types cover shapes
// INormalizationServiceClient doesn't expose typed. Mirrors Normalization's HSLOC entity
// (LantanaGroup.Link.Normalization.Domain.Entities.HSLOC) field-for-field.
public sealed class HslocReferenceCodeJson
{
    public Guid Id { get; set; }
    public string? HSLOCCode { get; set; }
    public string? CDCCode { get; set; }
    public string? ShortDescription { get; set; }
    public string? LongDescription { get; set; }
    public string? Version { get; set; }
    public bool IsActive { get; set; } = true;
}
