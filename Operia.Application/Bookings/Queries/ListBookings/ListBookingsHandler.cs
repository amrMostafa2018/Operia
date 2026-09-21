using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;
using Operia.Domain.Enums;

namespace Operia.Application.Bookings.Queries.ListBookings;

/// <summary>Executes the list bookings operation within the current tenant.</summary>
public sealed class ListBookingsHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope) : IRequestHandler<ListBookingsQuery, BookingListResult>
{
    /// <summary>Returns a filtered page and status summary from the caller's visible bookings.</summary>
    public async Task<BookingListResult> Handle(ListBookingsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = BookingAccess.RequireTenant(currentUser);
        var allowedBranches = await BookingAccess.GetAllowedBranchesAsync(currentUser, branchScope, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.BranchId))
        {
            BookingAccess.RequireBranch(allowedBranches, request.BranchId);
        }

        var query = db.Bookings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId &&
                        allowedBranches.Contains(x.BranchId) &&
                        x.ScheduledDate >= request.FromDate &&
                        x.ScheduledDate <= request.ToDate);

        query = ApplyFilters(query, request);

        if (currentUser.IsInRole(Roles.Staff))
        {
            var userId = currentUser.UserId!;
            query = query.Where(x => x.Employee != null && x.Employee.IdentityUserId == userId);
        }

        var counts = await query
            .GroupBy(_ => 1)
            .Select(group => new BookingSummaryDto(
                group.Count(),
                group.Count(x => x.Status == BookingStatus.Booked),
                group.Count(x => x.Status == BookingStatus.Completed),
                group.Count(x => x.Status == BookingStatus.Cancelled)))
            .SingleOrDefaultAsync(cancellationToken) ?? new BookingSummaryDto(0, 0, 0, 0);

        var rawItems = await query
            .OrderByDescending(x => x.ScheduledDate)
            .ThenByDescending(x => x.StartMinutes)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new
            {
                Booking = x,
                BranchName = x.Branch!.Name,
                EmployeeName = x.Employee!.FullName,
                Services = x.Items.OrderBy(item => item.CreatedAt).Select(item => new { item.Name, item.Type }).ToList()
            })
            .ToListAsync(cancellationToken);

        var items = rawItems
            .Select(x => new BookingListItemDto(
                x.Booking.Id,
                x.Booking.BookingNumber,
                x.Booking.CustomerName,
                x.Booking.CustomerMobile,
                string.Join(", ", x.Services.Select(s => s.Name)),
                x.Services.Select(s => s.Type.ToString()).FirstOrDefault() ?? "Service",
                x.Booking.BranchId,
                x.BranchName,
                x.Booking.EmployeeId,
                x.EmployeeName,
                x.Booking.ScheduledDate,
                x.Booking.StartMinutes,
                x.Booking.EndMinutes,
                x.Booking.Status.ToString(),
                Convert.ToBase64String(x.Booking.Version),
                x.Booking.PaymentMethod,
                x.Booking.TotalAmount,
                x.Booking.PaidAmount,
                x.Booking.DiscountAmount))
            .ToList();

        var totalPages = counts.Total == 0
            ? 0
            : (int)Math.Ceiling(counts.Total / (double)request.PageSize);

        return new BookingListResult(
            items,
            counts.Total,
            request.PageNumber,
            request.PageSize,
            totalPages,
            counts);
    }

    /// <summary>Applies booking filters to the already tenant- and branch-scoped query.</summary>
    private static IQueryable<Booking> ApplyFilters(IQueryable<Booking> query, ListBookingsQuery request)
    {
        if (!string.IsNullOrWhiteSpace(request.BranchId))
        {
            query = query.Where(x => x.BranchId == request.BranchId);
        }

        if (!string.IsNullOrWhiteSpace(request.EmployeeId))
        {
            query = query.Where(x => x.EmployeeId == request.EmployeeId);
        }

        if (Enum.TryParse<BookingStatus>(request.Status, true, out var status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x =>
                x.BookingNumber.Contains(search) ||
                x.CustomerName.Contains(search) ||
                x.CustomerMobile.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerMobile))
        {
            var mobile = request.CustomerMobile.Trim();
            query = query.Where(x => x.CustomerMobile.Contains(mobile));
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerName))
        {
            var name = request.CustomerName.Trim();
            query = query.Where(x => x.CustomerName.Contains(name));
        }

        return query;
    }
}
