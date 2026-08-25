namespace Operia.Application.Auth;

public static class Policies
{
    public const string PermissionClaimType = "permission";
    public const string AuthenticatedUser = "AuthenticatedUser";

    public const string DashboardRead = "Dashboard.Read";
    public const string DashboardExport = "Dashboard.Export";
    public const string BookingsRead = "Bookings.Read";
    public const string BookingsManage = "Bookings.Manage";
    public const string BookingsExport = "Bookings.Export";
    public const string BookingsCancel = "Bookings.Cancel";
    public const string BookingsReassign = "Bookings.Reassign";
    public const string BookingsChangeStatus = "Bookings.ChangeStatus";
    public const string CustomersRead = "Customers.Read";
    public const string CustomersManage = "Customers.Manage";
    public const string CustomersExport = "Customers.Export";
    public const string CustomersActivatePackage = "Customers.ActivatePackage";
    public const string CustomersCancelPackage = "Customers.CancelPackage";
    public const string CustomersAddPackage = "Customers.AddPackage";
    public const string RevenueRead = "Revenue.Read";
    public const string RevenueReview = "Revenue.Review";
    public const string RevenueExport = "Revenue.Export";
    public const string ReportsRead = "Reports.Read";
    public const string ReportsExport = "Reports.Export";
    public const string EmployeesRead = "Employees.Read";
    public const string EmployeesManage = "Employees.Manage";
    public const string EmployeesChangeStatus = "Employees.ChangeStatus";
    public const string EmployeesChangeRole = "Employees.ChangeRole";
    public const string PackagesRead = "Packages.Read";
    public const string PackagesManage = "Packages.Manage";
    public const string PackagesSell = "Packages.Sell";
    public const string OffersRead = "Offers.Read";
    public const string OffersManage = "Offers.Manage";
    public const string BranchesRead = "Branches.Read";
    public const string BranchesManage = "Branches.Manage";
    public const string SettingsManage = "Settings.Manage";
    public const string SettingsIdentityRead = "Settings.Identity.Read";
    public const string SettingsIdentityManage = "Settings.Identity.Manage";
    public const string SettingsPaymentsRead = "Settings.Payments.Read";
    public const string SettingsPaymentsManage = "Settings.Payments.Manage";
    public const string SettingsWorkingDaysRead = "Settings.WorkingDays.Read";
    public const string SettingsWorkingDaysManage = "Settings.WorkingDays.Manage";
    public const string SettingsSecurityRead = "Settings.Security.Read";
    public const string SettingsSecurityManage = "Settings.Security.Manage";
    public const string SettingsPasswordChange = "Settings.Password.Change";
    public const string SettingsUsersBan = "Settings.Users.Ban";
    public const string SettingsUsersDelete = "Settings.Users.Delete";
    public const string SettingsAccountDeactivate = "Settings.Account.Deactivate";
    public const string SettingsNotificationsManage = "Settings.Notifications.Manage";
    public const string SettingsLanguageManage = "Settings.Language.Manage";
    public const string SubscriptionsRead = "Subscriptions.Read";
    public const string SubscriptionsManage = "Subscriptions.Manage";
    public const string SubscriptionsExport = "Subscriptions.Export";
    public const string SubscriptionsInvoice = "Subscriptions.Invoice";
    public const string OnboardingManage = "Onboarding.Manage";
    public const string SupportRead = "Support.Read";
    public const string NotificationsRead = "Notifications.Read";

    public static readonly IReadOnlyList<string> TenantPermissionValues =
    [
        DashboardRead,
        DashboardExport,
        BookingsRead,
        BookingsManage,
        BookingsExport,
        BookingsCancel,
        BookingsReassign,
        BookingsChangeStatus,
        CustomersRead,
        CustomersManage,
        CustomersExport,
        CustomersActivatePackage,
        CustomersCancelPackage,
        CustomersAddPackage,
        RevenueRead,
        RevenueReview,
        RevenueExport,
        ReportsRead,
        ReportsExport,
        EmployeesRead,
        EmployeesManage,
        EmployeesChangeStatus,
        EmployeesChangeRole,
        PackagesRead,
        PackagesManage,
        PackagesSell,
        OffersRead,
        OffersManage,
        BranchesRead,
        BranchesManage,
        SettingsManage,
        SettingsIdentityRead,
        SettingsIdentityManage,
        SettingsPaymentsRead,
        SettingsPaymentsManage,
        SettingsWorkingDaysRead,
        SettingsWorkingDaysManage,
        SettingsSecurityRead,
        SettingsSecurityManage,
        SettingsPasswordChange,
        SettingsUsersBan,
        SettingsUsersDelete,
        SettingsAccountDeactivate,
        SettingsNotificationsManage,
        SettingsLanguageManage,
        SubscriptionsRead,
        SubscriptionsManage,
        SubscriptionsExport,
        SubscriptionsInvoice,
        OnboardingManage,
        SupportRead,
        NotificationsRead
    ];

    public static readonly IReadOnlyList<string> PermissionValues =
    [
        ..TenantPermissionValues,
        Platform.Manage
    ];

    public static class Platform
    {
        public const string Manage = "PlatformOperations.Manage";
    }
}
