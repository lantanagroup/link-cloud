using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business
{
    [ExcludeFromCodeCoverage]
    public class ResourceSearchModel
    {
        public string? Name { get; set; }   
        public Guid? ResourceId { get; set; }
        public List<string> Names { get; set; } = new();
    }
}
