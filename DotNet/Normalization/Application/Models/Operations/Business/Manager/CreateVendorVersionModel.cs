using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager
{
    [ExcludeFromCodeCoverage]
    public class CreateVendorVersionModel
    {
        required public Guid VendorId { get; set; }
        required public string Version { get; set; }
    }
}
