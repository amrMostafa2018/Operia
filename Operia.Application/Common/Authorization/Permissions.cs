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
        public const string Read = "Admins:read";
        public const string Write = "Admins:write";
    }
}
