using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query
{
    [ExcludeFromCodeCoverage]
    public class OperationSequenceSearchModel
    {
        public required string FacilityId { get; set; }
        public Guid? ResourceTypeId { get; set; }
        public string? ResourceType { get; set; }
    }
}
