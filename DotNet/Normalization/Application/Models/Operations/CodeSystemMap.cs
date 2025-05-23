namespace LantanaGroup.Link.Normalization.Application.Models.Operations
{
    public class CodeSystemMap
    {
        public int Id { get; set; }
        public string SourceSystem { get; set; }
        public string TargetSystem { get; set; }
        public Dictionary<string, CodeMap> CodeMaps { get; set; }

        public CodeSystemMap(string sourceSystem, string targetSystem, Dictionary<string, CodeMap> codeMaps)
        {
            SourceSystem = sourceSystem;
            TargetSystem = targetSystem;
            CodeMaps = codeMaps;
        }
    }
}
