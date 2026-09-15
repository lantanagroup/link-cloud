using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;

[ExcludeFromCodeCoverage]
[DataContract]
public class VendorVersionOperationPresetPostModel
{
    [Required]
    [DataMember]
    public Guid? VendorVersionId { get; set; }

    [Required]
    [DataMember]
    public Guid? OperationResourceTypeId { get; set; }
}
