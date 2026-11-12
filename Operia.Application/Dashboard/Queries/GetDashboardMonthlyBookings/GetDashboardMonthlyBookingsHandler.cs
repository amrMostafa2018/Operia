using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Revenue;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Dashboard.Queries.GetDashboardMonthlyBookings;

public sealed class GetDashboardMonthlyBookingsHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope,
    IDateTimeProvider clock) : IRequestHandler<GetDashboardMonthlyBookingsQuery, DashboardMonthlyBookingsDto>
{
    public async Task<DashboardMonthlyBookingsDto> Handle(
        GetDashboardMonthlyBookingsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var userId = currentUser.UserId
            ?? throw UnauthorizedException.FromCode(ApiErrorCodes.Auth.AuthenticationRequired, "detail");
        var periods = await new RevenuePeriods(db, currentUser, clock)
            .CurrentAsync(cancellationToken);
        var monthStart = periods.MonthStart;
        var monthEnd = monthStart.AddMonths(1);
        var allowed = await branchScope.GetAllowedBranchIdsAsync(userId, cancellationToken);
        var branches = allowed.ToArray();

        var bookings = db.Bookings.AsNoTracking()
            .Where(booking =>
                booking.TenantId == tenantId &&
                branches.Contains(booking.BranchId) &&
                booking.ScheduledDate >= monthStart &&
                booking.ScheduledDate < monthEnd &&
                booking.Status != BookingStatus.Cancelled);

        if (currentUser.IsInRole(Roles.Staff))
        {
            var assignedEmployeeIds = await db.Employees.AsNoTracking()
                .Where(employee =>
                    employee.TenantId == tenantId &&
                    employee.IdentityUserId == userId)
                .Select(employee => employee.Id)
                .ToArrayAsync(cancellationToken);
            bookings = bookings.Where(booking => assignedEmployeeIds.Contains(booking.EmployeeId));
        }

        var count = await bookings.CountAsync(cancellationToken);
        return new DashboardMonthlyBookingsDto(count);
    }
}
