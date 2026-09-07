namespace LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;

public class NormalizationOperationApiModel
{
    public Guid Id { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public string OperationJson { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDisabled { get; set; }
    public List<NormalizationOperationResourceTypeApiModel> OperationResourceTypes { get; set; } = [];
}

public class CreateNormalizationOperationRequestApiModel
{
    public List<string> ResourceTypes { get; set; } = [];
    public string? FacilityId { get; set; }
    public CreateNormalizationOperationDetailsApiModel Operation { get; set; } = new();
    public string? Description { get; set; }
    public List<Guid> VendorVersionIds { get; set; } = [];
}

public class CreateNormalizationOperationDetailsApiModel
{
    public string OperationType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // CopyProperty
    public string SourceFhirPath { get; set; } = string.Empty;
    public string TargetFhirPath { get; set; } = string.Empty;

    // ConditionalTransform
    public object? TargetValue { get; set; }
    public List<CreateNormalizationConditionApiModel>? Conditions { get; set; }

    // CodeMap
    public string? FhirPath { get; set; }
    public List<CreateNormalizationCodeSystemMapApiModel>? CodeSystemMaps { get; set; }

    // RemoveExtensions
    public List<string>? ExtensionUrls { get; set; }

    // CopyLocationAliasToTypeIteratively
    public int? MaxIterations { get; set; }
    public bool? SplitOnComma { get; set; }
}

public class CreateNormalizationConditionApiModel
{
    public string FhirPathSource { get; set; } = string.Empty;

    /// <summary>
    /// Numeric <c>ConditionOperator</c> value expected by the Normalization API
    /// (0=Equal, 1=GreaterThan, 2=GreaterThanOrEqual, 3=LessThan, 4=LessThanOrEqual, 5=NotEqual, 6=Exists, 7=NotExists).
    /// </summary>
    public int Operator { get; set; }

    public object? Value { get; set; }
}

public class CreateNormalizationCodeSystemMapApiModel
{
    public string SourceSystem { get; set; } = string.Empty;
    public string TargetSystem { get; set; } = string.Empty;
    public Dictionary<string, CreateNormalizationCodeMapEntryApiModel> CodeMaps { get; set; } = new();
}

public class CreateNormalizationCodeMapEntryApiModel
{
    public string Code { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
}

public class NormalizationOperationResourceTypeApiModel
{
    public Guid Id { get; set; }
    public Guid OperationId { get; set; }
    public Guid ResourceTypeId { get; set; }
    public NormalizationResourceApiModel? Resource { get; set; }
}

public class NormalizationResourceApiModel
{
    public Guid ResourceTypeId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
}

public class NormalizationOperationSequenceApiModel
{
    public Guid Id { get; set; }
    public int? Sequence { get; set; }
    public NormalizationOperationResourceTypeSequenceApiModel? OperationResourceType { get; set; }
}

public class NormalizationOperationResourceTypeSequenceApiModel
{
    public NormalizationOperationApiModel? Operation { get; set; }
    public NormalizationResourceApiModel? Resource { get; set; }
}

public class CreateNormalizationOperationSequenceApiModel
{
    public Guid? OperationId { get; set; }
    public int? Sequence { get; set; }
}

public class CreateNormalizationVendorVersionOperationPresetRequestApiModel
{
    public Guid? VendorVersionId { get; set; }
    public Guid? OperationResourceTypeId { get; set; }
}

public class NormalizationVendorVersionOperationPresetApiModel
{
    public Guid Id { get; set; }
    public Guid VendorVersionId { get; set; }
    public Guid OperationResourceTypeId { get; set; }
}

public class FacilityLocationApiModel
{
    public string Id { get; set; } = string.Empty;
    public string FacilityId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public string? PartOfId { get; set; }
    public string? LocationName { get; set; }
    public string? LocationAlias { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
}

public class CreateFacilityLocationRequestApiModel
{
    public string LocationId { get; set; } = string.Empty;
    public string? PartOfId { get; set; }
    public string? LocationName { get; set; }
    public string? LocationAlias { get; set; }
}

public class FacilityLocationLocalCodeMappingApiModel
{
    public string Id { get; set; } = string.Empty;
    public string FacilityId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public string? LocationName { get; set; }
    public string? LocationAlias { get; set; }
    public string LocalCodeSystem { get; set; } = string.Empty;
    public string LocalCode { get; set; } = string.Empty;
    public Guid? HSLOCId { get; set; }
    public string? HSLOCCode { get; set; }
    public string? HSLOCVersion { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
}

public class SearchFacilityLocationLocalCodeMappingsRequestApiModel
{
    public string? Id { get; set; }
    public string? FacilityId { get; set; }
    public string? LocationId { get; set; }
    public string? LocalCodeSystem { get; set; }
    public string? LocalCode { get; set; }
    public Guid? HSLOCId { get; set; }
    public bool? Unmapped { get; set; }
    public int? PageSize { get; set; }
    public int? PageNumber { get; set; }
}

public class CreateFacilityLocationLocalCodeMappingRequestApiModel
{
    public string LocationId { get; set; } = string.Empty;
    public string LocalCodeSystem { get; set; } = string.Empty;
    public string LocalCode { get; set; } = string.Empty;
    public Guid? HSLOCId { get; set; }
}

public class UpdateFacilityLocationLocalCodeMappingRequestApiModel
{
    public string LocalCodeSystem { get; set; } = string.Empty;
    public string LocalCode { get; set; } = string.Empty;
    public Guid? HSLOCId { get; set; }
}
