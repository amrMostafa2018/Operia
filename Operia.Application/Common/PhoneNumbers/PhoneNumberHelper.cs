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
            var value = phoneNumber.Trim();
            // Branches are currently configured for Egypt.  Accept both the
            // local form (e.g. 01050568043) shown in the admin UI and E.164.
            var parsed = PhoneUtil.Parse(value, value.StartsWith('+') ? null : "EG");
            return PhoneUtil.IsValidNumber(parsed);
        }
        catch (NumberParseException)
        {
            return false;
        }
    }

    public static string ToE164(string phoneNumber)
    {
        var value = phoneNumber.Trim();
        var parsed = PhoneUtil.Parse(value, value.StartsWith('+') ? null : "EG");
        return PhoneUtil.Format(parsed, PhoneNumberFormat.E164);
    }
}
