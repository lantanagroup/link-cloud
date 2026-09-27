namespace LantanaGroup.Link.Normalization.Application.Models.FacilityLocations;

public sealed record FacilityLocationTreeModel
{
    public string Id { get; init; } = "";
    public string LocationId { get; init; } = "";
    public string? PartOfId { get; init; }
    public string? LocationName { get; init; }
    public string? LocationAlias { get; init; }
    public List<FacilityLocationTreeMappingModel> Mappings { get; init; } = [];
}

public sealed record FacilityLocationTreeMappingModel
{
    public string Id { get; init; } = "";
    public string LocalCodeSystem { get; init; } = "";
    public string LocalCode { get; init; } = "";
    public Guid? HSLOCId { get; init; }
    public string? HSLOCCode { get; init; }
}