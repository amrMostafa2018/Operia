using PhoneNumbers;
using System.Text.RegularExpressions;

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

    public static bool IsValidMobile(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        try
        {
            var value = phoneNumber.Trim();
            var digits = string.Concat(value.Where(char.IsDigit));
            var isEgyptLocal = Regex.IsMatch(digits, @"^01[0125]\d{8}$");
            var hasSupportedDialCode = new[] { "20", "966", "971", "965" }
                .Any(code => digits.StartsWith(code, StringComparison.Ordinal));
            if (!isEgyptLocal && !hasSupportedDialCode)
                return false;

            var parsed = PhoneUtil.Parse(isEgyptLocal ? value : $"+{digits}", isEgyptLocal ? "EG" : null);
            if (!PhoneUtil.IsValidNumber(parsed))
                return false;

            if (parsed.CountryCode == 20)
                return Regex.IsMatch(parsed.NationalNumber.ToString(), @"^1[0125]\d{8}$");

            return PhoneUtil.GetNumberType(parsed) is PhoneNumberType.MOBILE or PhoneNumberType.FIXED_LINE_OR_MOBILE;
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
