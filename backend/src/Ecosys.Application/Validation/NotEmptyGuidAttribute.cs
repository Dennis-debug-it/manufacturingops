using System.ComponentModel.DataAnnotations;

namespace Ecosys.Application.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public NotEmptyGuidAttribute()
        : base("The {0} field must be a non-empty GUID.")
    {
    }

    public override bool IsValid(object? value) =>
        value is Guid guid && guid != Guid.Empty;
}
