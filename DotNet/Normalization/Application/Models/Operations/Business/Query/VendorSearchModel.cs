using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query
{
    [ExcludeFromCodeCoverage]
    public class VendorSearchModel
    {
        public Guid? VendorId { get; set; }
        public string? VendorName { get; set; }
    }
}
