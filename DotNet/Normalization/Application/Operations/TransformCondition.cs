namespace LantanaGroup.Link.Normalization.Application.Operations
{
    public class TransformCondition
    {
        public string FhirPathSource { get; set; }

        public ConditionOperator Operator { get; set; }

        public object Value { get; set; }

        private readonly ILogger<TransformCondition> _logger;

        public TransformCondition() { }
        public TransformCondition(string fhirPathSource, ConditionOperator @operator, object value = null, ILogger<TransformCondition> logger = null)
        {
            FhirPathSource = fhirPathSource;
            Operator = @operator;
            Value = value;
            _logger = logger;
        }
    }

    public enum ConditionOperator
    {
        Equal, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual, NotEqual, Exists, NotExists
    }
}