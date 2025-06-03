using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels
{
    [ExcludeFromCodeCoverage]
    public class PostOperationSequence
    {
        [DataMember]
        public required Guid OperationId { get; set; }
        [DataMember]
        public required int Sequence { get; set; } 
    }
}
