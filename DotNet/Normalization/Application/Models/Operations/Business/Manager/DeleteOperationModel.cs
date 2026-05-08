namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager
{
    public class DeleteOperationModel
    {
        public string? FacilityId { get; set; }
        public Guid? OperationId { get; set; }
        public string? ResourceType { get; set; }
        public Guid? VendorId { get; set; }
    }
}
