namespace Operia.Application.Common.Authorization;

public static class Permissions
{
    public const string ClaimType = "permission";

    public static class Staff
    {
        public const string Read = "Staff:read";
        public const string Write = "Staff:write";
    }

    public static class Admin
    {
        public const string DashboardRead = "Admins:Dashboard:read";
        public const string BookingRead = "Admins:Booking:read";
        public const string CustomersRead = "Admins:Customers:read";
        public const string ReportsRead = "Admins:Reports:read";
        public const string BranchesRead = "Admins:Branches:read";
        public const string BranchesManage = "Admins:Branches:manage";
        public const string EmployeesRead = "Admins:Employees:read";
        public const string EmployeesManage = "Admins:Employees:manage";
        public const string PackagesRead = "Admins:Packages:read";
        public const string SettingsRead = "Admins:Settings:read";
    }
}
