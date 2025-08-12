using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager
{
    [ExcludeFromCodeCoverage]
    public class CreateOperationSequencesModel
    {
        public required string FacilityId { get; set; }
        public required string ResourceType { get; set; }
        public List<CreateOperationSequenceModel> OperationSequences { get; set; } = new List<CreateOperationSequenceModel>();
    }

    [ExcludeFromCodeCoverage]
    public class CreateOperationSequenceModel
    {
        public required Guid OperationId { get; set; }
        public required int Sequence { get; set; }
    }
}
