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
                        null,
                        null,
                        null,
                        null,
                        false))
                    .ToList()))
            .ToListAsync(cancellationToken);

        var bookingIds = bookings.Select(x => x.Id).ToList();
        var reservationBalances = await db.BookingPackageReservations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId &&
                        bookingIds.Contains(x.BookingId) &&
                        x.CancellationAtUtc == null &&
                        x.CustomerPackage != null)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.CustomerPackageId)
            .Select(x => new PackageBalanceRow(
                x.BookingId,
                x.CustomerPackage!.CustomerId,
                x.CustomerPackage.PackageId,
                x.CustomerPackageId,
                x.CustomerPackage.Total,
                x.CustomerPackage.Used,
                x.CustomerPackage.ReservedSessions,
                x.CustomerPackage.Package != null ? x.CustomerPackage.Package.OfferType : OfferType.SingleSession,
                x.CustomerPackage.Package != null ? x.CustomerPackage.Package.PulseCount : null))
            .ToListAsync(cancellationToken);

        var reservationLookup = reservationBalances
            .GroupBy(x => (x.BookingId!, x.PackageId))
            .ToDictionary(
                group => group.Key,
                group => new Queue<PackageBalanceRow>(group));

        var customerIds = bookings.Select(x => x.CustomerId).Distinct().ToList();
        var packageIds = bookings
            .SelectMany(booking => booking.Items)
            .Where(item => item.Type == "package" && item.PackageId is not null)
            .Select(item => item.PackageId!)
            .Distinct()
            .ToList();

        var ownedBalances = packageIds.Count == 0 || customerIds.Count == 0
            ? []
            : await db.CustomerPackages
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId &&
                            customerIds.Contains(x.CustomerId) &&
                            packageIds.Contains(x.PackageId) &&
                            x.IsActive)
                .Select(x => new PackageBalanceRow(
                    null,
                    x.CustomerId,
                    x.PackageId,
                    x.Id,
                    x.Total,
                    x.Used,
                    x.ReservedSessions,
                    x.Package != null ? x.Package.OfferType : OfferType.SingleSession,
                    x.Package != null ? x.Package.PulseCount : null))
                .ToListAsync(cancellationToken);

        var ownedLookup = ownedBalances
            .GroupBy(x => (x.CustomerId, x.PackageId))
            .ToDictionary(
                group => group.Key,
                group => group.First());

        bookings = bookings.Select(booking =>
        {
            return booking with
            {
                Items = booking.Items.Select(item =>
                {
                    if (item.PackageId is null)
                    {
                        return item;
                    }

                    if (reservationLookup.TryGetValue((booking.Id, item.PackageId), out var reservedBalances) &&
                        reservedBalances.TryDequeue(out var reserved))
                    {
                        var enriched = EnrichPackageItem(item, reserved);
                        return item.Type switch
                        {
                            "package" => enriched with { PackageSessionLinked = true },
                            "session" => enriched with { PackageSessionLinked = item.UnitPrice == 0 },
                            _ => enriched
                        };
                    }

                    if (item.Type != "package")
                    {
                        return item;
                    }

                    if (ownedLookup.TryGetValue((booking.CustomerId, item.PackageId), out var owned))
                    {
                        // Zero-price lines are usage of the owned balance, not a billed repurchase.
                        return EnrichPackageItem(item, owned) with
                        {
                            PackageSessionLinked = item.UnitPrice == 0
                        };
                    }

                    return item;
                }).ToList()
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

    private static CalendarBookingItemDto EnrichPackageItem(CalendarBookingItemDto item, PackageBalanceRow balance) =>
        item with
        {
            CustomerPackageId = balance.CustomerPackageId,
            PackageRemainingSessions = CustomerPackageBalance.Remaining(
                balance.Total,
                balance.Used,
                balance.ReservedSessions,
                balance.OfferType,
                balance.PulseCount),
            PackagePulseCount = balance.PulseCount,
            PackageTotal = balance.Total,
            PackageUsed = balance.Used
        };

    private sealed record PackageBalanceRow(
        string? BookingId,
        string CustomerId,
        string PackageId,
        string CustomerPackageId,
        int Total,
        int Used,
        int? ReservedSessions,
        OfferType OfferType,
        int? PulseCount);
}
