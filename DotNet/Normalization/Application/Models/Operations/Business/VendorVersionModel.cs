
using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business
{
    [ExcludeFromCodeCoverage]
    public class VendorVersionModel
    {
        public Guid Id { get; set; }
        public Guid VendorId { get; set; }
        public string Version { get; set; } = string.Empty;
        public VendorModel? Vendor { get; set; }
    }
}
