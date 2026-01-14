using LantanaGroup.Link.Normalization.Application.Operations;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels
{
    [ExcludeFromCodeCoverage]
    public class PutOperationModel
    {
        [Required, DataMember]
        public required Guid? Id { get; set; }
        [Required, DataMember]
        public List<string> ResourceTypes { get; set; } = new List<string>();
        [Required, DataMember]
        public IOperation? Operation { get; set; }
        [DataMember]
        public bool IsDisabled { get; set; } = false;
        [DataMember]
        public string? FacilityId { get; set; }
        public List<Guid>? VendorIds { get; set; }
        public PutOperationModel(Guid? id, List<string> resourceTypes, IOperation operation, bool isDisabled, string? facilityId, List<Guid>? vendorIds)
        {
            Id = id;
            ResourceTypes = resourceTypes ?? new List<string>();
            Operation = operation;
            IsDisabled = isDisabled;
            FacilityId = facilityId;
            VendorIds = vendorIds;

            if (this.Operation.OperationType == OperationType.CopyLocation)
            {
                this.ResourceTypes.Add("Location");
            }
        }
    }
}
