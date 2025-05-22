using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Normalization.Application.Operations
{
    public class TransformCondition
    {
        public string Fhir_Path_Source { get; set; }

        public ConditionOperator Operator { get; set; }

        //TODO: Daniel - Should this be something other than a string?
        public string Value { get; set; }

        public TransformCondition(string Fhir_Path_Source, ConditionOperator Operator, string Value = "")
        {
            this.Fhir_Path_Source = Fhir_Path_Source;
            this.Operator = Operator;
            this.Value = Value;
        }

        public bool Is_Passed(DomainResource resource)
        {
            // Special handling for Exists and NotExists to avoid potential FHIRPath evaluation issues
            if (Operator == ConditionOperator.Exists || Operator == ConditionOperator.NotExists)
            {
                var elements = resource.Select(this.Fhir_Path_Source).ToList();
                bool exists = elements != null && elements.Any();
                return Operator == ConditionOperator.Exists ? exists : !exists;
            }

            var property = resource.Select(this.Fhir_Path_Source).FirstOrDefault();
            if (property == null || !property.Any())
            {
                return false;
            }

            var result = this.Operator switch
            {
                ConditionOperator.Equal => property.ToString().Equals(Value, StringComparison.OrdinalIgnoreCase),
                ConditionOperator.NotEqual => !property.ToString().Equals(Value, StringComparison.OrdinalIgnoreCase),
                ConditionOperator.GreaterThan => property.ToString().CompareTo(Value) > 0,
                ConditionOperator.GreaterThanOrEqual => property.ToString().CompareTo(Value) >= 0,
                ConditionOperator.LessThan => property.ToString().CompareTo(Value) < 0,
                ConditionOperator.LessThanOrEqual => property.ToString().CompareTo(Value) <= 0,
                _ => throw new InvalidOperationException($"Unsupported operator {Operator}")
            };

            return result;
        }
    }

    public enum ConditionOperator
    {
        Equal, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual, NotEqual, Exists, NotExists
    }
}