using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using System.Text.Json;

namespace LantanaGroup.Link.Normalization.Application.Services.Operations
{
    public class ConditionalTransformOperationService : BaseOperationService<ConditionalTransformOperation>
    {
        public ConditionalTransformOperationService(ILogger<ConditionalTransformOperationService> logger, TimeSpan? operationTimeout = null)
            : base(logger, operationTimeout)
        {
        }

        protected override OperationResult ExecuteOperation(ConditionalTransformOperation operation, DomainResource resource)
        {
            foreach (var condition in operation.Conditions)
            {
                if (!IsConditionPassed(condition, resource))
                    return OperationResult.Success(resource);
            }

            var result = SetTransformValue(resource, operation.TargetFhirPath, operation.TargetValue);
            return result;
        }

        private bool IsConditionPassed(TransformCondition condition, DomainResource resource)
        {
            if (condition.Operator == ConditionOperator.Exists || condition.Operator == ConditionOperator.NotExists)
            {
                var elements = resource.Select(condition.Fhir_Path_Source).ToList();
                bool exists = elements != null && elements.Any();
                return condition.Operator == ConditionOperator.Exists ? exists : !exists;
            }

            var scopedNode = resource.ToTypedElement();
            var sourceValueResult = OperationServiceHelper.ExtractValueFromFhirPath(scopedNode, condition.Fhir_Path_Source, Logger);
            object propertyValue = sourceValueResult.Success
                ? sourceValueResult.Value
                : OperationServiceHelper.GetValueReflectively(resource, condition.Fhir_Path_Source) ?? throw new InvalidOperationException($"No value found at {condition.Fhir_Path_Source}");

            var value = condition.Value is JsonElement jsonElement
                ? OperationServiceHelper.ConvertJsonElementToBaseType(jsonElement)
                : condition.Value;

            try
            {
                return propertyValue switch
                {
                    string strValue => CompareString(strValue, ConvertToString(value), condition.Operator),
                    int intValue => CompareNumber(intValue, ConvertToNumber<int>(value, typeof(int)), condition.Operator),
                    decimal decValue => CompareNumber(decValue, ConvertToNumber<decimal>(value, typeof(decimal)), condition.Operator),
                    double dblValue => CompareNumber(dblValue, ConvertToNumber<double>(value, typeof(double)), condition.Operator),
                    bool boolValue => CompareBoolean(boolValue, ConvertToBoolean(value), condition.Operator),
                    DateTime dateValue => CompareDateTime(dateValue, ConvertToDateTime(value), condition.Operator),
                    _ => throw new InvalidOperationException($"Unsupported property type {propertyValue.GetType().Name} for FHIRPath {condition.Fhir_Path_Source}.")
                };
            }
            catch (InvalidCastException ex)
            {
                Logger.LogError(ex, "Type conversion failed for value {Value} against FHIRPath {FhirPath}.", condition.Value, condition.Fhir_Path_Source);
                return false;
            }
        }

        private bool CompareString(string propertyValue, string conditionValue, ConditionOperator _operator)
        {
            if (conditionValue == null)
                return false;

            return _operator switch
            {
                ConditionOperator.Equal => propertyValue.Equals(conditionValue, StringComparison.OrdinalIgnoreCase),
                ConditionOperator.NotEqual => !propertyValue.Equals(conditionValue, StringComparison.OrdinalIgnoreCase),
                ConditionOperator.GreaterThan => propertyValue.CompareTo(conditionValue) > 0,
                ConditionOperator.GreaterThanOrEqual => propertyValue.CompareTo(conditionValue) >= 0,
                ConditionOperator.LessThan => propertyValue.CompareTo(conditionValue) < 0,
                ConditionOperator.LessThanOrEqual => propertyValue.CompareTo(conditionValue) <= 0,
                _ => throw new InvalidOperationException($"Operator {_operator} not supported for string comparisons.")
            };
        }

        private bool CompareNumber<T>(T propertyValue, T conditionValue, ConditionOperator _operator) where T : IComparable<T>
        {
            if (conditionValue == null)
                return false;

            return _operator switch
            {
                ConditionOperator.Equal => propertyValue.CompareTo(conditionValue) == 0,
                ConditionOperator.NotEqual => propertyValue.CompareTo(conditionValue) != 0,
                ConditionOperator.GreaterThan => propertyValue.CompareTo(conditionValue) > 0,
                ConditionOperator.GreaterThanOrEqual => propertyValue.CompareTo(conditionValue) >= 0,
                ConditionOperator.LessThan => propertyValue.CompareTo(conditionValue) < 0,
                ConditionOperator.LessThanOrEqual => propertyValue.CompareTo(conditionValue) <= 0,
                _ => throw new InvalidOperationException($"Operator {_operator} not supported for numerical comparisons.")
            };
        }

        private bool CompareBoolean(bool propertyValue, bool? conditionValue, ConditionOperator _operator)
        {
            if (!conditionValue.HasValue)
                return false;

            return _operator switch
            {
                ConditionOperator.Equal => propertyValue == conditionValue.Value,
                ConditionOperator.NotEqual => propertyValue != conditionValue.Value,
                _ => throw new InvalidOperationException($"Operator {_operator} not supported for boolean comparisons.")
            };
        }

        private bool CompareDateTime(DateTime propertyValue, DateTime? conditionValue, ConditionOperator _operator)
        {
            if (!conditionValue.HasValue)
                return false;

            return _operator switch
            {
                ConditionOperator.Equal => propertyValue == conditionValue.Value,
                ConditionOperator.NotEqual => propertyValue != conditionValue.Value,
                ConditionOperator.GreaterThan => propertyValue > conditionValue.Value,
                ConditionOperator.GreaterThanOrEqual => propertyValue >= conditionValue.Value,
                ConditionOperator.LessThan => propertyValue < conditionValue.Value,
                ConditionOperator.LessThanOrEqual => propertyValue <= conditionValue.Value,
                _ => throw new InvalidOperationException($"Operator {_operator} not supported for DateTime comparisons.")
            };
        }

        private string ConvertToString(object value)
        {
            return value switch
            {
                null => null,
                string str => str,
                int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                decimal d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                double dbl => dbl.ToString(System.Globalization.CultureInfo.InvariantCulture),
                bool b => b.ToString(),
                DateTime dt => dt.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                _ => throw new InvalidCastException($"Cannot convert {value.GetType().Name} to string.")
            };
        }

        private T ConvertToNumber<T>(object value, Type targetType) where T : IComparable<T>
        {
            if (value == null)
                return default;

            try
            {
                if (targetType == typeof(int))
                    return (T)(object)(value switch
                    {
                        int i => i,
                        string s => int.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
                        decimal d => (int)d,
                        double dbl => (int)dbl,
                        _ => throw new InvalidCastException($"Cannot convert {value.GetType().Name} to int.")
                    });
                if (targetType == typeof(decimal))
                    return (T)(object)(value switch
                    {
                        decimal d => d,
                        int i => (decimal)i,
                        string s => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
                        double dbl => (decimal)dbl,
                        _ => throw new InvalidCastException($"Cannot convert {value.GetType().Name} to decimal.")
                    });
                if (targetType == typeof(double))
                    return (T)(object)(value switch
                    {
                        double dbl => dbl,
                        int i => (double)i,
                        decimal d => (double)d,
                        string s => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
                        _ => throw new InvalidCastException($"Cannot convert {value.GetType().Name} to double.")
                    });
            }
            catch (FormatException ex)
            {
                throw new InvalidCastException($"Invalid number format for value {value}.", ex);
            }

            throw new InvalidCastException($"Unsupported number type {targetType.Name}.");
        }

        private bool? ConvertToBoolean(object value)
        {
            return value switch
            {
                null => null,
                bool b => b,
                string s => bool.TryParse(s, out bool result) ? result : throw new InvalidCastException($"Cannot convert string '{s}' to boolean."),
                _ => throw new InvalidCastException($"Cannot convert {value.GetType().Name} to boolean.")
            };
        }

        private DateTime? ConvertToDateTime(object value)
        {
            return value switch
            {
                null => null,
                DateTime dt => dt,
                string s => DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime result)
                    ? result
                    : throw new InvalidCastException($"Cannot convert string '{s}' to DateTime."),
                _ => throw new InvalidCastException($"Cannot convert {value.GetType().Name} to DateTime.")
            };
        }

        private OperationResult SetTransformValue(DomainResource resource, string targetFhirPath, object targetValue)
        {
            if (!OperationServiceHelper.ValidateFhirPath(targetFhirPath, resource, out var targetValidationError, Logger))
                return OperationResult.Failure($"Invalid target FHIRPath expression: {targetFhirPath}. {targetValidationError}", resource);

            var scopedNode = resource.ToTypedElement();
            var setResult = OperationServiceHelper.SetValueViaFhirPath(resource, targetFhirPath, targetValue, scopedNode, Logger);
            if (setResult.Result)
                return OperationResult.Success(resource);

            setResult = OperationServiceHelper.ResolveAndSetValueReflectively(resource, targetFhirPath, targetValue, Logger);
            if (setResult.Result)
                return OperationResult.Success(resource);

            setResult = OperationServiceHelper.CreateAndSetTargetElement(resource, targetFhirPath, targetValue, Logger);
            return setResult.Result
                ? OperationResult.Success(resource)
                : OperationResult.Failure(setResult.ErrorMessage, resource);
        }
    }
}