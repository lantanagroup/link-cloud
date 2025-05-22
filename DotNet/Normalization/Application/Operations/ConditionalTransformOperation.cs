namespace LantanaGroup.Link.Normalization.Application.Operations
{
    public class ConditionalTransformOperation<T> : IOperation
    {
        public OperationType OperationType => OperationType.ConditionalTransform;
        public string TargetFhirPath { get; private set; }
        public T TargetValue { get; private set; }
        public List<Condition> Conditions { get; private set; }

        public ConditionalTransformOperation(string targetFhirPath, T targetValue, List<Condition> conditions ) {
            TargetFhirPath = targetFhirPath;
            TargetValue = targetValue;
            Conditions = conditions;
        }
    }
}
