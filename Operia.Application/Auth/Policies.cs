namespace Operia.Application.Auth;

public static class Policies
{
    public const string PermissionClaimType = "permission";

    public const string DashboardRead = "Dashboard.Read";
    public const string BookingsRead = "Bookings.Read";
    public const string BookingsManage = "Bookings.Manage";
    public const string CustomersRead = "Customers.Read";
    public const string CustomersManage = "Customers.Manage";
    public const string RevenueRead = "Revenue.Read";
    public const string RevenueReview = "Revenue.Review";
    public const string ReportsRead = "Reports.Read";
    public const string EmployeesRead = "Employees.Read";
    public const string EmployeesManage = "Employees.Manage";
    public const string PackagesRead = "Packages.Read";
    public const string PackagesManage = "Packages.Manage";
    public const string BranchesRead = "Branches.Read";
    public const string BranchesManage = "Branches.Manage";
    public const string SettingsManage = "Settings.Manage";
    public const string SubscriptionsRead = "Subscriptions.Read";
    public const string SubscriptionsManage = "Subscriptions.Manage";
    public const string OnboardingManage = "Onboarding.Manage";

    public static class Platform
    {
        public const string Manage = "PlatformOperations.Manage";
    }
}
