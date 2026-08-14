using Operia.Domain.Entities;

namespace Operia.Application.Tests.Helpers;

internal static class BranchTestData
{
    public static Business Business(string tenantId, string? businessId = null) =>
        new()
        {
            Id = businessId ?? $"business-{tenantId}",
            TenantId = tenantId,
            ActivityName = $"Business {tenantId}"
        };

    public static Branch Branch(
        string id,
        string tenantId,
        string businessId,
        string name,
        string address = "Addr",
        string phoneNumber = "123") =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            BusinessId = businessId,
            Name = name,
            Address = address,
            PhoneNumber = phoneNumber,
            GoogleMapsUrl = "url"
        };

    public static Employee Employee(
        string id,
        string tenantId,
        string businessId,
        string identityUserId,
        string code,
        string fullName,
        string email,
        string mobileNumber,
        bool isActive = true) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            BusinessId = businessId,
            IdentityUserId = identityUserId,
            Code = code,
            FullName = fullName,
            Email = email,
            MobileNumber = mobileNumber,
            JoiningDate = DateOnly.FromDateTime(DateTime.Today),
            IsActive = isActive
        };
}
