using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Revenue;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Dashboard;

internal sealed record DashboardCardMetricsResult(
    int NewCustomersMonth,
    int TodayBookings,
    int MonthlyBookings,
    decimal TodayRevenue,
    decimal MonthlyRevenue,
    int PendingReviewCount);

/// <summary>Dashboard summary card calculations.</summary>
internal sealed class DashboardCardMetrics(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope,
    IDateTimeProvider clock)
{
    public async Task<DashboardCardMetricsResult> ComputeAsync(
        string? branchId,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var userId = currentUser.UserId
            ?? throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.AuthenticationRequired, "detail");

        var periods = await new RevenuePeriods(db, currentUser, clock)
            .CurrentAsync(cancellationToken);
        var allowed = await branchScope.GetAllowedBranchIdsAsync(userId, cancellationToken);
        if (branchId is not null && !allowed.Contains(branchId, StringComparer.Ordinal))
        {
            throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantAccessDenied, "branchId");
        }

        var branches = branchId is null ? allowed.ToArray() : [branchId];

        string[]? assignedEmployeeIds = null;
        if (currentUser.IsInRole(Roles.Staff))
        {
            assignedEmployeeIds = await db.Employees.AsNoTracking()
                .Where(employee =>
                    employee.TenantId == tenantId &&
                    employee.IdentityUserId == userId)
                .Select(employee => employee.Id)
                .ToArrayAsync(cancellationToken);
        }

        var monthStart = periods.MonthStart;
        var monthEnd = monthStart.AddMonths(1);
        var today = periods.Today;

        var newCustomersMonth = await db.Customers.AsNoTracking()
            .CountAsync(customer =>
                customer.TenantId == tenantId &&
                !customer.IsDirect &&
                customer.CreatedAt >= periods.MonthFromUtc &&
                customer.CreatedAt < periods.MonthToUtc,
                cancellationToken);

        var todayBookings = await CountBookingsAsync(
            tenantId, branches, assignedEmployeeIds,
            query => query.Where(booking => booking.ScheduledDate == today),
            cancellationToken);

        var monthlyBookings = await CountBookingsAsync(
            tenantId, branches, assignedEmployeeIds,
            query => query.Where(booking =>
                booking.ScheduledDate >= monthStart &&
                booking.ScheduledDate < monthEnd),
            cancellationToken);

        var ledger = await new RevenueLedger(db, currentUser, branchScope)
            .ScopedAsync(branchId, cancellationToken);
        var todayRevenue = await ledger.ForPeriodAsync(
            periods.TodayFromUtc, periods.TodayToUtc, cancellationToken);
        var monthRevenue = await ledger.ForPeriodAsync(
            periods.MonthFromUtc, periods.MonthToUtc, cancellationToken);
        var pendingReview = await PendingRevenuePaymentsCounter.CountAsync(
            db, currentUser, branchScope, branchId, cancellationToken);

        return new DashboardCardMetricsResult(
            newCustomersMonth,
            todayBookings,
            monthlyBookings,
            todayRevenue.Net,
            monthRevenue.Net,
            pendingReview);
    }

    private Task<int> CountBookingsAsync(
        string tenantId,
        string[] branches,
        string[]? assignedEmployeeIds,
        Func<IQueryable<Booking>, IQueryable<Booking>> applyDateFilter,
        CancellationToken cancellationToken)
    {
        var bookings = db.Bookings.AsNoTracking()
            .Where(booking =>
                booking.TenantId == tenantId &&
                branches.Contains(booking.BranchId) &&
                booking.Status != BookingStatus.Cancelled);

        if (assignedEmployeeIds is not null)
        {
            bookings = bookings.Where(booking => assignedEmployeeIds.Contains(booking.EmployeeId));
        }

        bookings = applyDateFilter(bookings);
        return bookings.CountAsync(cancellationToken);
    }
}
