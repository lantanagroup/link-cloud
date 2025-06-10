using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager
{
    [ExcludeFromCodeCoverage]
    public class UpdateOperationModel
    {
        public required Guid Id { get; set; }
        public required string OperationJson { get; set; }
        public required List<string> ResourceTypes { get; set; }
        public string? FacilityId { get; set; }
        public string? Description { get; set; }
        public bool IsDisabled { get; set; } = false;
    }
}
