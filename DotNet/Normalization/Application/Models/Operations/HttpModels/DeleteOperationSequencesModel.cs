using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels
{
    [ExcludeFromCodeCoverage]
    public class DeleteOperationSequencesModel
    {
        [Required, DataMember]
        public required string FacilityId { get; set; }
        [DataMember]
        public string? ResourceType { get; set; }
    }
}
