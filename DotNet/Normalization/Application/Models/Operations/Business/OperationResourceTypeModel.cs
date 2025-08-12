using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business
{
    [ExcludeFromCodeCoverage]
    public class OperationResourceTypeModel
    {
        public Guid Id { get; set; }
        public Guid OperationId { get; set; }
        public Guid ResourceTypeId { get; set; }    
        public OperationModel? Operation { get; set; }
        public ResourceModel? Resource { get; set; }
    }
}
