using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels
{
    [ExcludeFromCodeCoverage]
    public class VendorVersionOperationPresetPostModel
    {
        [Required]
        public required Guid VendorId { get; set; }
        [Required]
        public required Guid OperationResourceTypeId { get; set; }
    }
}
