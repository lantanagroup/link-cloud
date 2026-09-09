using LantanaGroup.Link.Normalization.Application.Models.Operations;

namespace LantanaGroup.Link.Normalization.Application.Operations
{
    public class HSLOCMapOperation : CodeMapOperation
    {
        public override OperationType OperationType => OperationType.HSLOCMap;

        public HSLOCMapOperation(List<CodeSystemMap> codeSystemMaps)
            : base("HSLOC Location Mapping", "type", codeSystemMaps, "Maps local Location codes to NHSN Healthcare Facility Patient Care Location (HSLOC) codes. Using this operation will also automatically enable CopyLocation operation and the CopyLocationAliasToTypeIteratively operation.")
        {

        }
    }
}