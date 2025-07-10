using LantanaGroup.Link.Normalization.Application.Operations;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels
{
    [ExcludeFromCodeCoverage]
    public class PutOperationModel()
    {
        [Required, DataMember]
        public required Guid Id { get; set; }
        [Required, DataMember]
        public List<string> ResourceTypes { get; set; } = new List<string>();
        [Required, DataMember]
        public IOperation? Operation { get; set; }
        [Required, DataMember]
        public bool IsDisabled { get; set; }
        [DataMember]
        public string? FacilityId { get; set; }
        [DataMember]
        public string? Description { get; set; }
        public List<Guid>? VendorIds { get; set; }
    }
}
