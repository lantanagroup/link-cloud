using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations
{
    [ExcludeFromCodeCoverage]
    public class CreateOperationModel
    {
        public required string OperationType { get; set; }
        public required string OperationJson { get; set; }
        public required List<string> ResourceTypes { get; set; }
        public string? FacilityId { get; set; }
        public string? Description { get; set; }
        public bool IsDisabled { get; set; } = false;
    }
}
