using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels
{
    [ExcludeFromCodeCoverage]
    public class PostOperationSequence
    {
        [Required, DataMember]
        public required Guid? OperationId { get; set; }
        [Required, Range(1, int.MaxValue), DataMember]
        public required int? Sequence { get; set; }
    }
}
