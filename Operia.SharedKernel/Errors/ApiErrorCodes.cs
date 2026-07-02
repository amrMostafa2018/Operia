namespace Operia.SharedKernel.Errors;

/// <summary>
/// Error code names — must match <c>Name</c> in api-errors.en.json / api-errors.ar.json
/// and <c>ERRORS.*</c> keys in the frontend i18n files.
/// </summary>
public static class ApiErrorCodes
{
    public static class Auth
    {
        public const string EmailAlreadyRegistered = "EmailAlreadyRegistered";
        public const string EmailRequired = "EmailRequired";
        public const string EmailInvalid = "EmailInvalid";
        public const string PasswordRequired = "PasswordRequired";
        public const string PasswordMinLength = "PasswordMinLength";
        public const string PasswordMismatch = "PasswordMismatch";
        public const string PhoneRequired = "PhoneRequired";
        public const string PhoneInvalid = "PhoneInvalid";
        public const string PhoneAlreadyRegistered = "PhoneAlreadyRegistered";
        public const string ConfirmPasswordRequired = "ConfirmPasswordRequired";
        public const string OtpPhoneRequired = "OtpPhoneRequired";
        public const string OtpInvalid = "OtpInvalid";
        public const string OtpExpired = "OtpExpired";
        public const string IdentityError = "IdentityError";
    }
}
