using LantanaGroup.Link.Normalization.Application.Operations;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels
{
    [ExcludeFromCodeCoverage]
    public class PostOperationModel
    {
        [Required, DataMember]
        public List<string> ResourceTypes { get; set; } = new List<string>();
        [Required, DataMember]
        public required IOperation Operation { get; set; }
        [DataMember]
        public string? FacilityId { get; set; } = null;
        [DataMember(IsRequired = false)]
        public List<Guid>? VendorIds { get; set; }

        public PostOperationModel(List<string> resourceTypes, IOperation operation, string? facilityId, List<Guid>? vendorIds)
        {
            ResourceTypes = resourceTypes ?? new List<string>();
            Operation = operation;
            FacilityId = facilityId;
            VendorIds = vendorIds;

            if (this.Operation.OperationType == OperationType.CopyLocation)
            {
                this.ResourceTypes.Add(ResourceType.Location.ToString());
            }
        }
    }
}
