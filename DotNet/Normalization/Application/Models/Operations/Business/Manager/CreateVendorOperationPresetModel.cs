using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager
{
    [ExcludeFromCodeCoverage]
    public class CreateVendorOperationPresetModel
    {
        public string? Vendor { get; set; }
        public string? Versions { get; set; }
        public string? Description { get; set; }
    }
}
