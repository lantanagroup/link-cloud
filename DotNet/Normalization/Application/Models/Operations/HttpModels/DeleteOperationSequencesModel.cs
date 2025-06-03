using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels
{
    [ExcludeFromCodeCoverage]
    public class DeleteOperationSequencesModel
    {
        public required string FacilityId { get; set; }
        public string? ResourceType { get; set; }
    }
}
