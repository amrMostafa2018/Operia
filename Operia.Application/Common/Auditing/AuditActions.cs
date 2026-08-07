namespace Operia.Application.Common.Auditing;

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
}
