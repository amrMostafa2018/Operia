using PhoneNumbers;

namespace Operia.Application.Common.PhoneNumbers;

public static class PhoneNumberHelper
{
    private static readonly PhoneNumberUtil PhoneUtil = PhoneNumberUtil.GetInstance();

    public static bool IsValid(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        try
        {
            var parsed = PhoneUtil.Parse(phoneNumber.Trim(), null);
            return PhoneUtil.IsValidNumber(parsed);
        }
        catch (NumberParseException)
        {
            return false;
        }
    }

    public static string ToE164(string phoneNumber)
    {
        var parsed = PhoneUtil.Parse(phoneNumber.Trim(), null);
        return PhoneUtil.Format(parsed, PhoneNumberFormat.E164);
    }
}
