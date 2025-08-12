using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business
{
    [ExcludeFromCodeCoverage]
    public class VendorVersionOperationPresetModel
    {
        public Guid Id { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime? ModifyDate { get; set; }
        public Guid VendorVersionId { get; internal set; }
        public Guid OperationResourceTypeId { get; internal set; }
        public OperationResourceTypeModel OperationResourceType { get; set; } = new();
        public VendorVersionModel VendorVersion { get; set; } = new();
    }
}
