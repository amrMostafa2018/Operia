using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Authorization;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Bookings.Queries.GetEligibleBookingEmployees;

/// <summary>Finds branch staff eligible for all selected offers and free for the selected appointment.</summary>
public sealed class GetEligibleBookingEmployeesHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope,
    IIdentityService identity,
    IDateTimeProvider clock)
    : IRequestHandler<GetEligibleBookingEmployeesQuery, IReadOnlyList<EligibleBookingEmployee>>
{
    public async Task<IReadOnlyList<EligibleBookingEmployee>> Handle(
        GetEligibleBookingEmployeesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = BookingAccess.RequireTenant(currentUser);
        var allowed = await BookingAccess.GetAllowedBranchesAsync(currentUser, branchScope, cancellationToken);
        BookingAccess.RequireBranch(allowed, request.BranchId);

        var ids = request.Items.Select(x => x.PackageId).Distinct().ToArray();
        var offers = await db.Packages.AsNoTracking().Include(x => x.AvailableStaff)
            .Where(x => x.TenantId == tenantId && ids.Contains(x.Id) && x.Status == PackageStatus.Active)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (offers.Count != ids.Length)
        {
            throw ApiNotFoundException.FromCode(ApiErrorCodes.Packages.PackageNotFound, "items");
        }

        var end = request.EndMinutes;
        var dayCode = request.ScheduledDate.DayOfWeek.ToString()[..3].ToLowerInvariant();
        var startTime = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(request.StartMinutes));
        var endTime = end == 1440 ? TimeOnly.MaxValue : TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(end));
        var staff = await db.Employees.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive &&
                        x.UserBranches.Any(branch => branch.BranchId == request.BranchId))
            .Where(x => !currentUser.IsInRole(Roles.Staff) || x.IdentityUserId == currentUser.UserId)
            .Select(x => new { x.Id, x.IdentityUserId, x.FullName, x.Code })
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);
        var staffIds = staff.Select(x => x.Id).ToArray();
        var scheduledIds = await db.EmployeeWorkingDays.AsNoTracking()
            .Where(x => x.TenantId == tenantId && staffIds.Contains(x.EmployeeId) &&
                        x.BranchId == request.BranchId && x.Day == dayCode && x.Enabled &&
                        x.FromTime <= startTime && x.ToTime >= endTime)
            .Select(x => x.EmployeeId).ToListAsync(cancellationToken);
        var scheduled = scheduledIds.ToHashSet(StringComparer.Ordinal);
        var roles = await identity.GetRolesAsync(staff.Select(x => x.IdentityUserId), cancellationToken);
        var candidateIds = staff
            .Where(x => scheduled.Contains(x.Id))
            .Where(x => string.Equals(roles.GetValueOrDefault(x.IdentityUserId), Roles.Staff,
                StringComparison.OrdinalIgnoreCase))
            .Where(x => offers.Values.All(offer => offer.AllStaff || offer.AvailableStaff.Count == 0 ||
                offer.AvailableStaff.Any(assigned => assigned.EmployeeId == x.Id)))
            .Select(x => x.Id).ToArray();

        var busy = await db.Bookings.AsNoTracking()
            .Where(x => x.TenantId == tenantId && candidateIds.Contains(x.EmployeeId) &&
                        x.ScheduledDate == request.ScheduledDate && x.Status != BookingStatus.Cancelled &&
                        request.StartMinutes < x.EndMinutes && end > x.StartMinutes)
            .Select(x => x.EmployeeId).ToListAsync(cancellationToken);
        busy.AddRange(await db.BookingHolds.AsNoTracking()
            .Where(x => x.TenantId == tenantId && candidateIds.Contains(x.EmployeeId) &&
                        x.ScheduledDate == request.ScheduledDate && x.ConvertedBookingId == null &&
                        x.ExpiresAtUtc > clock.UtcNow && request.StartMinutes < x.EndMinutes &&
                        end > x.StartMinutes)
            .Select(x => x.EmployeeId).ToListAsync(cancellationToken));
        var unavailable = busy.ToHashSet(StringComparer.Ordinal);
        return staff.Where(x => candidateIds.Contains(x.Id) && !unavailable.Contains(x.Id))
            .Select(x => new EligibleBookingEmployee(x.Id, x.FullName)).ToList();
    }
}
