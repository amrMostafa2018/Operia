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
        public const string UserNameAlreadyRegistered = "UserNameAlreadyRegistered";
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
        public const string BranchNotAssigned = "EmployeeScheduleBranchNotAssigned";
        public const string OverlappingBranchHours = "EmployeeScheduleOverlappingBranchHours";
    }

    public static class Settings
    {
        public const string GalleryPhotoLimitExceeded = "GalleryPhotoLimitExceeded";
        public const string BusinessProfileRequired = "BusinessProfileRequired";
    }

    public static class Packages
    {
        public const string CategoryNameRequired = "CATEGORY_NAME_REQUIRED";
        public const string CategoryNameMax = "CATEGORY_NAME_MAX";
        public const string CategoryIconRequired = "CATEGORY_ICON_REQUIRED";
        public const string CategoryNameTaken = "CATEGORY_NAME_TAKEN";
        public const string SubCategoryNameRequired = "SUB_CATEGORY_NAME_REQUIRED";
        public const string SubCategoryNameMax = "SUB_CATEGORY_NAME_MAX";
        public const string SubCategoryParentRequired = "SUB_CATEGORY_PARENT_REQUIRED";
        public const string SubCategoryParentNotFound = "SUB_CATEGORY_PARENT_NOT_FOUND";
        public const string SubCategoryNameTaken = "SUB_CATEGORY_NAME_TAKEN";
        public const string PackageNameRequired = "PACKAGE_NAME_REQUIRED";
        public const string PackageNameMax = "PACKAGE_NAME_MAX";
        public const string PackageDescriptionMax = "PACKAGE_DESCRIPTION_MAX";
        public const string PackageCategoryRequired = "PACKAGE_CATEGORY_REQUIRED";
        public const string PackageDurationRequired = "PACKAGE_DURATION_REQUIRED";
        public const string PackageDurationMin = "PACKAGE_DURATION_MIN";
        public const string PackageSessionCountRequired = "PACKAGE_SESSION_COUNT_REQUIRED";
        public const string PackageSessionCountMin = "PACKAGE_SESSION_COUNT_MIN";
        public const string PackagePulseCountMin = "PACKAGE_PULSE_COUNT_MIN";
        public const string PackagePriceRequired = "PACKAGE_PRICE_REQUIRED";
        public const string PackagePriceMin = "PACKAGE_PRICE_MIN";
        public const string PackageDiscountPercentRange = "PACKAGE_DISCOUNT_PERCENT_RANGE";
        public const string PackageNotFound = "PACKAGE_NOT_FOUND";
        public const string PackageAlreadyCancelled = "PACKAGE_ALREADY_CANCELLED";
    }
}
