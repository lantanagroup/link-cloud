using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using Hl7.FhirPath;

namespace LantanaGroup.Link.Normalization.Application.Operations
{
    public class Condition
    {
        public string Fhir_Path_Source { get; set; }

        public ConditionOperators Operator { get; set; }

        //TODO: Daniel - Should this be something other than a string?
        public string Value { get; set; }

        public Condition(string fhir_Path_Source, ConditionOperators conditionOperator, string value="")
        {
            Fhir_Path_Source = fhir_Path_Source;
            Operator = conditionOperator;
            Value = value;
        }

        public bool Is_Passed(DomainResource resource)
        {
            var property = resource.Select(this.Fhir_Path_Source).FirstOrDefault();

            if (!property.Any())
            {
                return false;
            }

            var result = this.Operator switch
            {
                ConditionOperators.Equal => property.ToString().Equals(Value, StringComparison.OrdinalIgnoreCase),
                ConditionOperators.NotEqual => !property.ToString().Equals(Value, StringComparison.OrdinalIgnoreCase),
                ConditionOperators.GreaterThan => property.ToString().CompareTo(Value) > 0,
                ConditionOperators.GreaterThanOrEqual => property.ToString().CompareTo(Value) >= 0,
                ConditionOperators.LessThan => property.ToString().CompareTo(Value) < 0,
                ConditionOperators.LessThanOrEqual => property.ToString().CompareTo(Value) <= 0,
                ConditionOperators.Exists => property != null,
                ConditionOperators.NotExists => property == null,
                _ => throw new Exception()
            };


            return result;
        }
    }

    public enum ConditionOperators
    {
        Equal, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual, NotEqual, Exists, NotExists
    }
}
