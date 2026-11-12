using PaperTrail.Domain.Exceptions;

namespace PaperTrail.Domain.Common;

public static class DomainRules
{
    public const long MaxNumber = 999999999999999999;

    public static long PositiveNumber(long value, string field)
    {
        if (value is <= 0 or > MaxNumber)
            throw new DomainException($"{field} must be a positive number with at most 18 digits.");
        return value;
    }

    public static string RequiredText(string? value, int maxLength, string field)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > maxLength)
            throw new DomainException($"{field} is required and must not exceed {maxLength} characters.");
        return text;
    }
}
