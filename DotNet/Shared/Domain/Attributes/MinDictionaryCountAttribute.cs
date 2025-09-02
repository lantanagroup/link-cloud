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
        if (value is IDictionary dict && dict.Count >= _minCount)
            return ValidationResult.Success;

        return new ValidationResult($"The dictionary must contain at least {_minCount} item(s).");
    }
}
