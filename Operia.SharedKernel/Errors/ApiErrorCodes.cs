namespace Operia.SharedKernel.Errors;

/// <summary>
/// Error code names — must match <c>Name</c> in api-errors.en.json / api-errors.ar.json
/// and <c>ERRORS.*</c> keys in the frontend i18n files.
/// </summary>
public static class ApiErrorCodes
{
    public static class Auth
    {
        public const string AuthenticationRequired = "AuthenticationRequired";
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
        public const string FullNameRequired = "FullNameRequired";
        public const string FullNameMinLength = "FullNameMinLength";
        public const string OtpPhoneRequired = "OtpPhoneRequired";
        public const string OtpInvalid = "OtpInvalid";
        public const string OtpExpired = "OtpExpired";
        public const string IdentityError = "IdentityError";
    }

    public static class Access
    {
        public const string TenantContextRequired = "TenantContextRequired";
        public const string TenantAccessDenied = "TenantAccessDenied";
    }

    public static class EmployeeSchedule
    {
        public const string OutsideBusinessHours = "EmployeeScheduleOutsideBusinessHours";
    }

    public static class Settings
    {
        public const string GalleryPhotoLimitExceeded = "GalleryPhotoLimitExceeded";
        public const string BusinessProfileRequired = "BusinessProfileRequired";
    }
}
