using LantanaGroup.Link.Normalization.Application.Models.Operations;
using Microsoft.Identity.Client;

namespace LantanaGroup.Link.Normalization.Application.Operations
{
    public class CopyLocationOperation : IOperation
    {
        public OperationType OperationType => OperationType.CopyLocation;

        public string Name { get; set; }
        public string Description { get; set; }

        public CopyLocationOperation() {
            Name = "Copy Location Operation";
            Description = "Copies each Location Identifier 'System' and 'Value' fields into Location.Type as a CodeableConcept";
        }
    }
}
