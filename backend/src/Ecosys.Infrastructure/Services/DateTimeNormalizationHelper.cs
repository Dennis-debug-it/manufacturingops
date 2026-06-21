namespace Ecosys.Infrastructure.Services;

public static class DateTimeNormalizationHelper
{
    public static DateTime NormalizeToUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        return value.ToUniversalTime();
    }

    public static DateTime? NormalizeNullableToUtc(DateTime? value) =>
        value.HasValue ? NormalizeToUtc(value.Value) : null;
}
