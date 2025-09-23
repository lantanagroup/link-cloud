using Google.Protobuf.WellKnownTypes;
using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.Shared.Domain.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public class MinDictionaryCountAttribute : ValidationAttribute
{
    private readonly int _minCount;
    public MinDictionaryCountAttribute(int minCount)
    {
        if (minCount < 0) throw new ArgumentOutOfRangeException(nameof(minCount), "minCount must be non-negative.");
            _minCount = minCount;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // Allow null unless [Required] is also specified.
        if (value is null) return ValidationResult.Success;

        // Non-generic IDictionary (e.g., Hashtable)
        if (value is IDictionary dict)
            return dict.Count >= _minCount
                ? ValidationResult.Success
                : new ValidationResult(FormatErrorMessage(validationContext.DisplayName ?? validationContext.MemberName ?? "Value"));

        // Generic IDictionary<,> or IReadOnlyDictionary<,> (e.g., Dictionary<,>, ImmutableDictionary<,>)
        var type = value.GetType();
        foreach (var i in type.GetInterfaces())
        {
            if (!i.IsGenericType) continue;
            var g = i.GetGenericTypeDefinition();
            if (g == typeof(IDictionary<,>) || g == typeof(IReadOnlyDictionary<,>))
            {
                var countProp = type.GetProperty("Count");
                if (countProp?.GetMethod != null && countProp.GetValue(value) is int c)
                    return c >= _minCount
                        ? ValidationResult.Success
                        : new ValidationResult(FormatErrorMessage(validationContext.DisplayName ?? validationContext.MemberName ?? "Value"));
                break;
            }
        }

        // Not a dictionary type — treat as invalid to surface misapplication.
        return new ValidationResult(FormatErrorMessage(validationContext.DisplayName ?? validationContext.MemberName ?? "Value"));
    }

    public override string FormatErrorMessage(string name) =>
        string.Format(ErrorMessageString ?? "The field {0} must contain at least {1} item(s).", name, _minCount);
}
