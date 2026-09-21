using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Bookings.Common;

/// <summary>Centralizes tenant and branch access checks used by booking operations.</summary>
internal static class BookingAccess
{
    /// <summary>Returns the authenticated tenant or fails when the tenant context is missing.</summary>
    public static string RequireTenant(ICurrentUserService currentUser) =>
        !string.IsNullOrWhiteSpace(currentUser.TenantId)
            ? currentUser.TenantId
            : throw UnauthorizedException.FromCode(ApiErrorCodes.Access.TenantContextRequired, "detail");

    /// <summary>Loads branch access for the authenticated user before booking data is read.</summary>
    public static async Task<IReadOnlyCollection<string>> GetAllowedBranchesAsync(
        ICurrentUserService currentUser,
        IBranchScope branchScope,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.AuthenticationRequired, "detail");
        }

        return await branchScope.GetAllowedBranchIdsAsync(currentUser.UserId, cancellationToken);
    }

    /// <summary>Rejects a booking branch outside the user's allowed set.</summary>
    public static void RequireBranch(IReadOnlyCollection<string> allowedBranchIds, string branchId)
    {
        if (!allowedBranchIds.Contains(branchId, StringComparer.Ordinal))
        {
            throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantAccessDenied, "branchId");
        }
    }
}
