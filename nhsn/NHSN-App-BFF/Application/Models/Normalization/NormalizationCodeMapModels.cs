using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Normalization;

public sealed class CodeMapOperationJson
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? FhirPath { get; set; }
    public List<CodeSystemMapJson> CodeSystemMaps { get; set; } = [];
}

public sealed class CodeSystemMapJson
{
    public string SourceSystem { get; set; } = string.Empty;
    public string TargetSystem { get; set; } = string.Empty;
    public Dictionary<string, CodeMapEntryJson> CodeMaps { get; set; } = new();
}

public sealed class CodeMapEntryJson
{
    public string Code { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
}

public sealed class UpdateNormalizationOperationRequestApiModel
{
    public required Guid Id { get; init; }
    public required List<string> ResourceTypes { get; init; }
    public string? FacilityId { get; init; }
    public required CreateNormalizationOperationDetailsApiModel Operation { get; init; }
    public bool IsDisabled { get; init; }
    public List<Guid>? VendorVersionIds { get; init; }
}
