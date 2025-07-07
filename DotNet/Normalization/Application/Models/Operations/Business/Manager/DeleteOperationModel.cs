

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager
{
    public class DeleteOperationModel
    {
        public string? FacilityId { get; internal set; }
        public Guid? OperationId { get; internal set; }
        public string? ResourceType { get; internal set; }
        public Guid? VendorId { get; internal set; }
    }
}
