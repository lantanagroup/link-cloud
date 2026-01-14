using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business
{
    [ExcludeFromCodeCoverage]
    public class OperationModel
    {
        public Guid Id { get; set; }
        public string FacilityId { get; set; } = string.Empty;
        public string OperationJson { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsDisabled { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime? ModifyDate { get; set; }
        public List<VendorVersionOperationPresetModel> VendorPresets { get; set; } = new List<VendorVersionOperationPresetModel>();
        public List<OperationResourceTypeModel> OperationResourceTypes { get; set; } = new();
    }
}
