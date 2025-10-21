using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;

[ExcludeFromCodeCoverage]
public class VendorVersionOperationPresetSearchModel
{
    public Guid? Id { get; set; }
    public Guid? VendorVersionId { get; set; }
    public string? Resource { get; set; }
}