using Hl7.FhirPath;

namespace LantanaGroup.Link.Normalization.Application.Operations
{
    public class ConditionalTransformOperation : IOperation
    {
        public OperationType OperationType => OperationType.ConditionalTransform;

        public string Name { get; set; }
        public string Description { get; set; }

        public string TargetFhirPath { get; private set; }

        public object TargetValue { get; private set; }

        public List<TransformCondition> Conditions { get; private set; }

        public ConditionalTransformOperation(string name, string targetFhirPath, object targetValue, List<TransformCondition> conditions, string description = "")
        {
            Name = name;
            TargetFhirPath = targetFhirPath;
            TargetValue = targetValue;
            Conditions = conditions;
            Description = description;

            // Validate FHIRPath syntax
            try
            {
                var parser = new FhirPathCompiler();
                parser.Compile(TargetFhirPath);
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
            {
                throw new ArgumentException($"Invalid FHIRPath syntax: {ex.Message}", ex);
            }
        }
    }
}