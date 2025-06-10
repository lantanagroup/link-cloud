using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business
{
    [ExcludeFromCodeCoverage]
    public class OperationResourceTypeModel
    {
        public OperationModel Operation { get; set; }   
        public ResourceModel Resource { get; set; }
        public object VendorPresets { get; internal set; }
    }
}
