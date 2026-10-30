namespace Operia.SharedKernel.Errors;

/// <summary>
/// Error code names — must match <c>Name</c> in api-errors.en.json / api-errors.ar.json
/// and <c>ERRORS.*</c> keys in the frontend i18n files.
/// </summary>
public static class ApiErrorCodes
{
    /// <summary>Authentication and account validation failures.</summary>
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

    /// <summary>Tenant and resource access failures.</summary>
    public static class Access
    {
        public const string TenantContextRequired = "TenantContextRequired";
        public const string TenantAccessDenied = "TenantAccessDenied";
    }

    /// <summary>Employee working-day and branch schedule failures.</summary>
    public static class EmployeeSchedule
    {
        public const string OutsideBusinessHours = "EmployeeScheduleOutsideBusinessHours";
        public const string BranchNotAssigned = "EmployeeScheduleBranchNotAssigned";
        public const string OverlappingBranchHours = "EmployeeScheduleOverlappingBranchHours";
    }

    /// <summary>Business settings and profile prerequisite failures.</summary>
    public static class Settings
    {
        public const string GalleryPhotoLimitExceeded = "GalleryPhotoLimitExceeded";
        public const string BusinessProfileRequired = "BusinessProfileRequired";
    }

    /// <summary>Service, offer, and Package validation failures.</summary>
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
        public const string PackageSessionOrPulseRequired = "PACKAGE_SESSION_OR_PULSE_REQUIRED";
        public const string PackageSessionAndPulseBothFilled = "PACKAGE_SESSION_AND_PULSE_BOTH_FILLED";
        public const string PackagePulseCountMin = "PACKAGE_PULSE_COUNT_MIN";
        public const string PackagePriceRequired = "PACKAGE_PRICE_REQUIRED";
        public const string PackagePriceMin = "PACKAGE_PRICE_MIN";
        public const string PackageCashPricePositive = "PACKAGE_CASH_PRICE_POSITIVE";
        public const string PackageDiscountPercentRange = "PACKAGE_DISCOUNT_PERCENT_RANGE";
        public const string PackagePaymentOptionRequired = "PACKAGE_PAYMENT_OPTION_REQUIRED";
        public const string PackageInstallmentPriceRequired = "PACKAGE_INSTALLMENT_PRICE_REQUIRED";
        public const string PackageInstallmentPriceMin = "PACKAGE_INSTALLMENT_PRICE_MIN";
        public const string PackageInstallmentOnlyPackage = "PACKAGE_INSTALLMENT_ONLY_PACKAGE";
        public const string PackageInstallmentPayloadNotAllowed = "PACKAGE_INSTALLMENT_PAYLOAD_NOT_ALLOWED";
        public const string PackageFirstPaymentRequired = "PACKAGE_FIRST_PAYMENT_REQUIRED";
        public const string PackageFirstPaymentRange = "PACKAGE_FIRST_PAYMENT_RANGE";
        public const string PackageInstallmentCountRange = "PACKAGE_INSTALLMENT_COUNT_RANGE";
        public const string PackageInstallmentRowsCount = "PACKAGE_INSTALLMENT_ROWS_COUNT";
        public const string PackageInstallmentAmountInvalid = "PACKAGE_INSTALLMENT_AMOUNT_INVALID";
        public const string PackageInstallmentTotalMismatch = "PACKAGE_INSTALLMENT_TOTAL_MISMATCH";
        public const string PackageInstallmentSessionsInvalid = "PACKAGE_INSTALLMENT_SESSIONS_INVALID";
        public const string PackageInstallmentReleaseCap = "PACKAGE_INSTALLMENT_RELEASE_CAP";
        public const string PackageCashUnavailable = "PACKAGE_CASH_UNAVAILABLE";
        public const string PackageStaffInvalid = "PACKAGE_STAFF_INVALID";
        public const string PackageStaffUnavailable = "PACKAGE_STAFF_UNAVAILABLE";
        public const string PackageInstallmentUnavailable = "PACKAGE_INSTALLMENT_UNAVAILABLE";
        public const string PackageNotFound = "PACKAGE_NOT_FOUND";
        public const string PackageAlreadyCancelled = "PACKAGE_ALREADY_CANCELLED";
    }

    /// <summary>Booking access, availability, state, and reservation failures.</summary>
    public static class Bookings
    {
        public const string BranchRequired = "BookingBranchRequired";
        public const string DateRangeInvalid = "BookingDateRangeInvalid";
        public const string DateRangeTooLarge = "BookingDateRangeTooLarge";
        public const string NotFound = "BookingNotFound";
        public const string SlotUnavailable = "BookingSlotUnavailable";
        public const string Changed = "BookingChanged";
        public const string StatusInvalid = "BookingStatusInvalid";
        public const string CustomerNotFound = "BookingCustomerNotFound";
        public const string EmployeeNotFound = "BookingEmployeeNotFound";
        public const string EmployeeNotInBranch = "BookingEmployeeNotInBranch";
        public const string OutsideWorkingHours = "BookingOutsideWorkingHours";
        public const string OnePackageSessionRequired = "BookingOnePackageSessionRequired";
        public const string ReusedServiceQuantityOne = "BookingReusedServiceQuantityOne";
        public const string OwnedPackageRequired = "BookingOwnedPackageRequired";
        public const string PackageSessionUnavailable = "BookingPackageSessionUnavailable";
        public const string TerminalReadOnly = "BookingTerminalReadOnly";
        public const string PackageEditNotAllowed = "BookingPackageEditNotAllowed";
        public const string PaymentMethodUnavailable = "BookingPaymentMethodUnavailable";
        public const string InstallmentPurchaseOnly = "BookingInstallmentPurchaseOnly";
        public const string InstallmentEditNotAllowed = "BookingInstallmentEditNotAllowed";
        public const string InstallmentCollectionInvalid = "BookingInstallmentCollectionInvalid";
        public const string ItemsRequired = "BookingItemsRequired";
        public const string NotBookedStatus = "BookingNotBookedStatus";
        public const string PulsesExceedRemaining = "PulsesExceedRemaining";
        public const string CloseItemsIncomplete = "CloseItemsIncomplete";
    }
}
