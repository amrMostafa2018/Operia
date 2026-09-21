namespace Operia.Application.Common.Auditing;

/// <summary>Names auditable business actions with stable codes.</summary>
public static class AuditActions
{
    public const string BranchCreated = "BranchCreated";
    public const string BranchUpdated = "BranchUpdated";
    public const string BranchDeleted = "BranchDeleted";

    public const string EmployeeCreated = "EmployeeCreated";
    public const string EmployeeUpdated = "EmployeeUpdated";
    public const string EmployeeRoleChanged = "RoleChanged";
    public const string EmployeeStatusChanged = "StatusChanged";
    public const string EmployeeScheduleUpdated = "EmployeeScheduleUpdated";

    public const string BusinessIdentityUpdated = "BusinessIdentityUpdated";
    public const string PaymentMethodsUpdated = "PaymentMethodsUpdated";
    public const string WorkingDaysUpdated = "WorkingDaysUpdated";
    public const string SecuritySettingsUpdated = "SecuritySettingsUpdated";
    public const string PasswordChanged = "PasswordChanged";
    public const string TenantUserBanned = "TenantUserBanned";
    public const string TenantUserDeleted = "TenantUserDeleted";
    public const string AccountDeactivated = "AccountDeactivated";

    public const string ServiceCategoryCreated = "ServiceCategoryCreated";
    public const string SubServiceCategoryCreated = "SubServiceCategoryCreated";
    public const string PackageCreated = "PackageCreated";
    public const string PackageUpdated = "PackageUpdated";
    public const string PackageDeleted = "PackageDeleted";

    /// <summary>Booking creation committed with its reservations.</summary>
    public const string BookingCreated = "BookingCreated";
    /// <summary>Booking items or payment choice changed.</summary>
    public const string BookingUpdated = "BookingUpdated";
    /// <summary>Booking cancelled and unused reservations released.</summary>
    public const string BookingCancelled = "BookingCancelled";
}
