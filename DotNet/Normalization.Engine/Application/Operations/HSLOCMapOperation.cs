using LantanaGroup.Link.Normalization.Application.Models.Operations;

namespace LantanaGroup.Link.Normalization.Application.Operations
{
    public class HSLOCMapOperation : CodeMapOperation
    {
        public override OperationType OperationType => OperationType.HSLOCMap;

        public HSLOCMapOperation(string name, List<CodeSystemMap> codeSystemMaps, string description = "")
            : base(name, "type", codeSystemMaps, description)
        {
        }
    }
}