using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Bookings.Queries.GetBookingHistory;

/// <summary>Executes the get booking history operation within the current tenant.</summary>
public sealed class GetBookingHistoryHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope) : IRequestHandler<GetBookingHistoryQuery, IReadOnlyList<BookingHistoryDto>>
{
    /// <summary>Returns the audit timeline for a booking visible to the caller.</summary>
    public async Task<IReadOnlyList<BookingHistoryDto>> Handle(
        GetBookingHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = BookingAccess.RequireTenant(currentUser);
        var bookingQuery = db.Bookings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == request.BookingId);
        if (currentUser.IsInRole(Roles.Staff))
        {
            var userId = currentUser.UserId!;
            bookingQuery = bookingQuery.Where(x =>
                x.Employee != null && x.Employee.IdentityUserId == userId);
        }

        var branchId = await bookingQuery
            .Select(x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken);

        if (branchId is null)
        {
            throw ApiNotFoundException.FromCode(ApiErrorCodes.Bookings.NotFound);
        }

        var allowedBranches = await BookingAccess.GetAllowedBranchesAsync(currentUser, branchScope, cancellationToken);
        BookingAccess.RequireBranch(allowedBranches, branchId);

        return await db.BookingHistory
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.BookingId == request.BookingId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new BookingHistoryDto(
                x.Id,
                x.Action,
                x.ChangedByUserId,
                x.ChangedByDisplayName,
                x.CreatedAt,
                x.ChangesJson))
            .ToListAsync(cancellationToken);
    }
}
