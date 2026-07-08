using LantanaGroup.Link.Normalization.Application.Models.Operations;

namespace LantanaGroup.Link.Normalization.Application.Operations
{
    public class CopyLocationAliasToTypeIterativelyOperation : IOperation
    {
        public OperationType OperationType => OperationType.CopyLocationAliasToTypeIteratively;

        public string Name { get; set; }
        public string Description { get; set; }
        public int MaxIterations { get; set; } = 15;
        public bool SplitOnComma { get; set; } = false;

        public CopyLocationAliasToTypeIterativelyOperation()
        {
            Name = "Copy Location Alias to Type Iteratively Operation";
            Description = "Copies Location Alias fields into Location.Type as a CodeableConcept. This also copies all parent Locations' aliases in the partOf hierarchy.";
        }
    }
}
