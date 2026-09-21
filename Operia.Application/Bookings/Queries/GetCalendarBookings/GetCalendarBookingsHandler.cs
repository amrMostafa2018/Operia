using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Enums;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Bookings.Queries.GetCalendarBookings;

/// <summary>Executes the get calendar bookings operation within the current tenant.</summary>
public sealed class GetCalendarBookingsHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope,
    IDateTimeProvider clock) : IRequestHandler<GetCalendarBookingsQuery, CalendarBookingsResult>
{
    /// <summary>Returns visible bookings and active holds for a branch and date range.</summary>
    public async Task<CalendarBookingsResult> Handle(GetCalendarBookingsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = BookingAccess.RequireTenant(currentUser);
        var allowedBranches = await BookingAccess.GetAllowedBranchesAsync(currentUser, branchScope, cancellationToken);
        BookingAccess.RequireBranch(allowedBranches, request.BranchId);

        var query = db.Bookings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId &&
                        x.BranchId == request.BranchId &&
                        x.ScheduledDate >= request.FromDate &&
                        x.ScheduledDate <= request.ToDate);

        if (!string.IsNullOrWhiteSpace(request.EmployeeId))
        {
            query = query.Where(x => x.EmployeeId == request.EmployeeId);
        }

        if (currentUser.IsInRole(Roles.Staff))
        {
            var userId = currentUser.UserId!;
            query = query.Where(x => x.Employee != null && x.Employee.IdentityUserId == userId);
        }

        var bookings = await query
            .OrderBy(x => x.ScheduledDate)
            .ThenBy(x => x.StartMinutes)
            .Select(x => new CalendarBookingDto(
                x.Id,
                x.BookingNumber,
                x.Status.ToString(),
                x.CustomerId,
                x.CustomerName,
                x.CustomerMobile,
                x.EmployeeId,
                x.Employee!.FullName,
                x.BranchId,
                x.Branch!.Name,
                x.ScheduledDate,
                x.StartMinutes,
                x.EndMinutes,
                x.Source,
                x.PaymentMethod,
                x.TotalAmount,
                x.PaidAmount,
                x.DiscountAmount,
                x.CreatedAt,
                Convert.ToBase64String(x.Version),
                x.Items
                    .OrderBy(item => item.CreatedAt)
                    .Select(item => new CalendarBookingItemDto(
                        item.Id,
                        item.Name,
                        item.Type == BookingItemType.PackageSession ? "package" :
                            item.Type == BookingItemType.UnlistedService ? "unlisted" : "session",
                        item.Quantity,
                        item.DurationMinutes,
                        item.UnitPrice,
                        item.PackageId,
                        null,
                        null))
                    .ToList()))
            .ToListAsync(cancellationToken);

        var bookingIds = bookings.Select(x => x.Id).ToList();
        var packageBalances = await db.BookingPackageReservations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId &&
                        bookingIds.Contains(x.BookingId) &&
                        x.ReleasedAtUtc == null &&
                        x.CustomerPackage != null &&
                        x.Booking != null &&
                        x.Booking.Items.Any(item =>
                            item.Type == BookingItemType.PackageSession &&
                            item.PackageId == x.CustomerPackage.PackageId))
            .Select(x => new
            {
                x.BookingId,
                x.CustomerPackageId,
                Remaining = x.CustomerPackage == null
                    ? 0
                    : Math.Max(
                        x.CustomerPackage.TotalSessions -
                        x.CustomerPackage.UsedSessions -
                        x.CustomerPackage.ReservedSessions,
                        0)
            })
            .ToDictionaryAsync(x => x.BookingId, cancellationToken);

        bookings = bookings.Select(booking =>
        {
            packageBalances.TryGetValue(booking.Id, out var balance);
            return booking with
            {
                Items = booking.Items.Select(item => item.Type == "package" && balance is not null
                    ? item with
                    {
                        CustomerPackageId = balance.CustomerPackageId,
                        PackageRemainingSessions = balance.Remaining
                    }
                    : item).ToList()
            };
        }).ToList();

        var holdsQuery = db.BookingHolds
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId &&
                        x.BranchId == request.BranchId &&
                        x.ScheduledDate >= request.FromDate &&
                        x.ScheduledDate <= request.ToDate &&
                        x.ConvertedBookingId == null &&
                        x.ExpiresAtUtc > clock.UtcNow);

        if (!string.IsNullOrWhiteSpace(request.EmployeeId))
        {
            holdsQuery = holdsQuery.Where(x => x.EmployeeId == request.EmployeeId);
        }

        if (currentUser.IsInRole(Roles.Staff))
        {
            var userId = currentUser.UserId!;
            holdsQuery = holdsQuery.Where(x => x.Employee != null && x.Employee.IdentityUserId == userId);
        }

        var holds = await holdsQuery
            .OrderBy(x => x.ScheduledDate)
            .ThenBy(x => x.StartMinutes)
            .Select(x => new CalendarBookingHoldDto(
                x.Id,
                x.EmployeeId,
                x.ScheduledDate,
                x.StartMinutes,
                x.EndMinutes,
                x.ExpiresAtUtc))
            .ToListAsync(cancellationToken);

        return new CalendarBookingsResult(bookings, holds, clock.UtcNow);
    }
}
