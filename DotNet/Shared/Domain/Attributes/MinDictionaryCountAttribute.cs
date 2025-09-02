using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.Shared.Domain.Attributes;

public class MinDictionaryCountAttribute : ValidationAttribute
{
    private readonly int _minCount;
    public MinDictionaryCountAttribute(int minCount)
    {
        _minCount = minCount;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is IDictionary dict && dict.Count >= _minCount)
            return ValidationResult.Success;

        return new ValidationResult($"The dictionary must contain at least {_minCount} item(s).");
    }
}
