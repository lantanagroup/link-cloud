using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business
{
    [ExcludeFromCodeCoverage]
    public class OperationSequenceModel
    {
        public Guid Id { get; set; }
        public string FacilityId { get; set; } = string.Empty;
        public int Sequence { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime? ModifyDate { get; set; }
        public OperationResourceTypeModel OperationResourceType { get; set; } = new OperationResourceTypeModel();
        public List<VendorOperationPresetModel> VendorPresets { get; set; } = new List<VendorOperationPresetModel>();
    }
}
