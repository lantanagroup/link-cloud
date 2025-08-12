using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business
{
    [ExcludeFromCodeCoverage]
    public class ResourceModel
    {
        public Guid ResourceTypeId { get; set; }
        public string ResourceName { get; set; } = string.Empty;
    }
}
