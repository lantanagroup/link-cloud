
using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business
{
    [ExcludeFromCodeCoverage]
    public class VendorModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<VendorVersionModel> Versions { get; set; }
    }
}
