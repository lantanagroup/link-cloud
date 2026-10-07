using System.ComponentModel.DataAnnotations;

namespace Automation.UI.Models;

public sealed class IdRequest
{
    [NotEmptyGuid]
    public Guid Id { get; set; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
internal sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public NotEmptyGuidAttribute()
    {
        ErrorMessage = "Id must be a non-empty GUID.";
    }

    public override bool IsValid(object? value)
    {
        return value is Guid id && id != Guid.Empty;
    }
}
