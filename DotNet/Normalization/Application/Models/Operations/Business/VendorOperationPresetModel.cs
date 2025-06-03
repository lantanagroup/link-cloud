using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business
{
    [ExcludeFromCodeCoverage]
    public class VendorOperationPresetModel
    {
        public Guid Id { get; set; }
        public string? Vendor { get; set; }
        public string? Versions { get; set; }
        public string? Description { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime? ModifyDate { get; set; }
    }
}
