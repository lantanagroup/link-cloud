using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager
{
    [ExcludeFromCodeCoverage]
    public class CreateVendorVersionOperationPresetModel
    {
        public Guid VendorVersionId { get; set; }      
        public Guid OperationResourceTypeId { get; set; }
    }
}
