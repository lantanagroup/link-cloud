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
        public IOperation Operation { get; set; }
        [DataMember]
        public string? FacilityId { get; set; } = null;
        [DataMember(IsRequired = false)]
        public List<Guid>? VendorVersionIds { get; set; }

        public PostOperationModel(List<string> resourceTypes, IOperation operation, string? facilityId, List<Guid>? vendorVersionIds)
        {
            ResourceTypes = resourceTypes ?? new List<string>();
            Operation = operation;
            FacilityId = facilityId;
            VendorVersionIds = vendorVersionIds;

            if ((this.Operation.OperationType == OperationType.CopyLocation ||
                this.Operation.OperationType == OperationType.CopyLocationAliasToTypeIteratively) &&
                !this.ResourceTypes.Contains(ResourceType.Location.ToString()))
            {
                this.ResourceTypes.Add(ResourceType.Location.ToString());
            }
        }
    }
}
