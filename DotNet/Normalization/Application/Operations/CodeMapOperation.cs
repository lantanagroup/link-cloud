using Hl7.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models.Operations;

namespace LantanaGroup.Link.Normalization.Application.Operations
{
    public class CodeMapOperation : IOperation
    {
        public OperationType OperationType => OperationType.CodeMap;
        public string Name { get; set; }
        public string Description { get; set; }
        public string FhirPath { get; private set; }
        public List<CodeSystemMap> CodeSystemMaps { get; private set; }

        public CodeMapOperation(string name, string fhirPath, List<CodeSystemMap> codeSystemMaps, string description = "")
        {
            Name = name;
            FhirPath = fhirPath;
            CodeSystemMaps = codeSystemMaps;
            Description = description;

            // Validate FHIRPath syntax
            try
            {
                var parser = new FhirPathCompiler();
                parser.Compile(FhirPath);
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
            {
                throw new ArgumentException($"Invalid FHIRPath syntax: {ex.Message}", ex);
            }
        }
    }
}
