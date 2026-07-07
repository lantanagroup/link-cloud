using LantanaGroup.Link.Normalization.Application.Models.Operations;
using Microsoft.Identity.Client;

namespace LantanaGroup.Link.Normalization.Application.Operations
{
    public class CopyLocationAliasToTypeIterativelyOperation : IOperation
    {
        public OperationType OperationType => OperationType.CopyLocation;

        public string Name { get; set; }
        public string Description { get; set; }

        public CopyLocationAliasToTypeIterativelyOperation()
        {
            Name = "Copy Location Alias to Type Iteratively Operation";
            Description = "Copies Location Alias fields into Location.Type as a CodeableConcept. This also copies all parent Locations' aliases in the partOf hierarchy.";
        }
    }
}
