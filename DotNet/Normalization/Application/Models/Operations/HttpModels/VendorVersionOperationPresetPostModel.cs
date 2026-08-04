using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;

[ExcludeFromCodeCoverage]
public class VendorVersionOperationPresetPostModel
{
    [Required]
    public Guid? VendorVersionId { get; set; }

    [Required]
    public Guid? OperationResourceTypeId { get; set; }
}
